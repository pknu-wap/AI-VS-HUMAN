import { readFile } from "node:fs/promises";

const marker = "<!-- ai-pr-review:openai -->";
const githubToken = requireEnv("GITHUB_TOKEN");
const openaiApiKey = requireEnv("OPENAI_API_KEY");
const repository = requireEnv("GITHUB_REPOSITORY");
const eventPath = requireEnv("GITHUB_EVENT_PATH");
const model = process.env.OPENAI_MODEL || "gpt-5.1";
const maxPatchChars = Number(process.env.AI_REVIEW_MAX_PATCH_CHARS || 60000);

try {
  await run();
} catch (error) {
  console.error("AI review failed, but this helper should not block the PR.");
  console.error(error);
}

async function run() {
  const event = JSON.parse(await readFile(eventPath, "utf8"));
  const [owner, repo] = repository.split("/");
  const pullRequest = await resolvePullRequest(event, owner, repo);
  const prNumber = pullRequest.number;

  const changedFiles = await fetchChangedFiles(owner, repo, prNumber);
  const reviewInput = buildReviewInput(changedFiles, maxPatchChars);

  if (!reviewInput.trim()) {
    await upsertPrComment(
      owner,
      repo,
      prNumber,
      `${marker}\n## AI Senior Review\n\nThere is no text diff to review. Check whether this PR only changes binary files, metadata, or deletions.`
    );
    return;
  }

  const prompt = [
    `Repository: ${repository}`,
    `Pull request: #${prNumber} ${pullRequest.title}`,
    `Author: ${pullRequest.user?.login ?? "unknown"}`,
    `Base: ${pullRequest.base?.ref ?? "unknown"}`,
    `Head: ${pullRequest.head?.ref ?? "unknown"}`,
    "",
    "PR body:",
    pullRequest.body || "(no description)",
    "",
    "Changed files and patches:",
    reviewInput,
  ].join("\n");

  const review = await createReview(prompt).catch(async (error) => {
    console.error("OpenAI review generation failed.");
    console.error(error);

    await upsertPrComment(
      owner,
      repo,
      prNumber,
      [
        marker,
        "## AI Senior Review",
        "",
        "AI review could not be generated. Check the Actions log for the exact OpenAI API error, then verify `OPENAI_API_KEY`, `OPENAI_MODEL`, project billing, and model access.",
      ].join("\n")
    ).catch((commentError) => {
      console.error("Failed to post AI review failure notice.");
      console.error(commentError);
    });

    return null;
  });

  if (!review) {
    return;
  }

  await upsertPrComment(owner, repo, prNumber, `${marker}\n${review.trim()}\n`);
}

function requireEnv(name) {
  const value = process.env[name];
  if (!value) {
    throw new Error(`Missing required environment variable: ${name}`);
  }
  return value;
}

async function githubApi(path, options = {}) {
  const response = await fetch(`https://api.github.com${path}`, {
    ...options,
    headers: {
      Accept: "application/vnd.github+json",
      Authorization: `Bearer ${githubToken}`,
      "X-GitHub-Api-Version": "2022-11-28",
      ...(options.headers || {}),
    },
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(`GitHub API ${response.status} for ${path}: ${body}`);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

async function fetchChangedFiles(owner, repo, prNumber) {
  const files = [];

  for (let page = 1; page <= 10; page += 1) {
    const batch = await githubApi(
      `/repos/${owner}/${repo}/pulls/${prNumber}/files?per_page=100&page=${page}`
    );

    files.push(...batch);

    if (batch.length < 100) {
      break;
    }
  }

  return files;
}

async function fetchPullRequest(owner, repo, prNumber) {
  return githubApi(`/repos/${owner}/${repo}/pulls/${prNumber}`);
}

async function resolvePullRequest(event, owner, repo) {
  if (event.pull_request) {
    return event.pull_request;
  }

  const rawPrNumber = event.inputs?.pr_number;

  if (!rawPrNumber) {
    throw new Error("Missing workflow_dispatch input: pr_number");
  }

  const prNumber = Number(rawPrNumber);

  if (!Number.isInteger(prNumber) || prNumber <= 0) {
    throw new Error(`Invalid pull request number: ${rawPrNumber}`);
  }

  return fetchPullRequest(owner, repo, prNumber);
}

function buildReviewInput(files, limit) {
  let remaining = limit;
  const chunks = [];

  for (const file of files) {
    const patch = file.patch || "";
    const header = [
      `---`,
      `file: ${file.filename}`,
      `status: ${file.status}`,
      `additions: ${file.additions}`,
      `deletions: ${file.deletions}`,
    ].join("\n");

    if (shouldSkipPatch(file.filename, patch)) {
      chunks.push(`${header}\npatch: (skipped: generated, metadata, binary, or too noisy for useful review)`);
      continue;
    }

    const block = `${header}\n${patch || "patch: (not available)"}`;

    if (block.length > remaining) {
      chunks.push(`${header}\npatch: (truncated because the PR diff exceeded the review budget)`);
      break;
    }

    chunks.push(block);
    remaining -= block.length;
  }

  return chunks.join("\n\n");
}

function shouldSkipPatch(filename, patch) {
  const lower = filename.toLowerCase();

  if (lower.endsWith(".meta")) {
    return true;
  }

  if (/\.(png|jpg|jpeg|gif|psd|wav|mp3|ogg|fbx|blend|ttf|otf|assetbundle)$/i.test(lower)) {
    return true;
  }

  if (patch.length > 20000 && !/\.(cs|asmdef|json|yml|yaml|xml|shader|compute)$/i.test(lower)) {
    return true;
  }

  return false;
}

async function createReview(input) {
  const instructions = [
    "You are a strict senior code reviewer for a Unity/C# game project.",
    "Write the review in Korean.",
    "Be direct and demanding, but stay professional.",
    "Prioritize correctness, regressions, null-reference risks, Unity lifecycle mistakes, prefab/scene serialization issues, performance problems, concurrency/timing issues, and missing tests or validation.",
    "Do not waste space on style nits unless they hide a real bug.",
    "If the diff is too small to prove a bug, clearly say what must be verified instead of pretending certainty.",
    "Use Markdown.",
    "Start with '## AI Senior Review'.",
    "Use Korean section headings equivalent to: Required fixes, Strong suspicions / verification needed, Test requirements.",
    "Every finding must name the file and the relevant changed code context when possible.",
    "If there are no blocking issues, say so explicitly and still list the riskiest verification points.",
  ].join("\n");

  const response = await fetch("https://api.openai.com/v1/responses", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${openaiApiKey}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      model,
      instructions,
      input,
      max_output_tokens: 3000,
    }),
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(`OpenAI API ${response.status}: ${body}`);
  }

  const data = await response.json();
  const text = extractResponseText(data);

  if (!text.trim()) {
    throw new Error("OpenAI response did not include review text.");
  }

  return text;
}

function extractResponseText(data) {
  if (typeof data.output_text === "string") {
    return data.output_text;
  }

  return (data.output || [])
    .flatMap((item) => item.content || [])
    .map((content) => content.text || "")
    .join("\n");
}

async function upsertPrComment(owner, repo, prNumber, body) {
  const comments = await githubApi(
    `/repos/${owner}/${repo}/issues/${prNumber}/comments?per_page=100`
  );
  const existing = comments.find((comment) => comment.body?.includes(marker));

  if (existing) {
    await githubApi(`/repos/${owner}/${repo}/issues/comments/${existing.id}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ body }),
    });
    return;
  }

  await githubApi(`/repos/${owner}/${repo}/issues/${prNumber}/comments`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ body }),
  });
}
