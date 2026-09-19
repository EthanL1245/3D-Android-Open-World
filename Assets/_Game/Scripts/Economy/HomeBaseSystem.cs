using UnityEngine;

public class HomeBaseSystem : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform fishMarketPoint;
    [SerializeField] private Transform tankShopPoint;
    [SerializeField] private float interactionRadius = 3.2f;

    public Transform FishMarketPoint => fishMarketPoint;
    public Transform TankShopPoint => tankShopPoint;

    public void Configure(
        Transform playerTransform,
        Transform marketPoint,
        Transform shopPoint)
    {
        player = playerTransform;
        fishMarketPoint = marketPoint;
        tankShopPoint = shopPoint;
    }

    public bool IsNearFishMarket()
    {
        return IsNear(
            fishMarketPoint
        );
    }

    public bool IsNearTankShop()
    {
        return IsNear(
            tankShopPoint
        );
    }

    private bool IsNear(Transform target)
    {
        if (player == null ||
            target == null)
        {
            return false;
        }

        Vector3 delta =
            player.position -
            target.position;

        delta.y = 0f;

        return delta.sqrMagnitude <=
               interactionRadius *
               interactionRadius;
    }
}
