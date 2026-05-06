// 데미지를 받을 수 있는 오브젝트가 공통으로 구현하는 인터페이스
// 플레이어 무기나 탄환은 이 인터페이스를 통해 적/보스/드론을 같은 방식으로 공격한다.
public interface IDamageable
{
    // 받은 데미지만큼 체력을 깎는다.
    void TakeDamage(float damage);
}
