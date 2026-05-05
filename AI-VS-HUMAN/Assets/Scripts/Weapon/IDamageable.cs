/// <summary>
/// 데미지를 받을 수 있는 오브젝트 인터페이스
/// AssaultRifle에서 TryGetComponent로 이걸 찾아서 데미지를 줌
/// </summary>
public interface IDamageable
{
    void TakeDamage(float damage);
}
