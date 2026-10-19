using UnityEngine;

// Adds up ticket sales as people join the crowd. The game is set 30 years from now,
// so prices are entered in today's money and inflated to show-day money.
public class ConcertRevenue : MonoBehaviour
{
    [SerializeField] private AudienceSpawner _audience;

    [SerializeField] private Vector2 _ticketPriceRange = new Vector2(450.0f, 1200.0f);
    [SerializeField] private int _yearsAhead = 30;
    [SerializeField] private float _annualInflation = 0.06f;
    [SerializeField] private float _walkUpMarkup = 1.5f;
    [SerializeField] private bool _countStartingCrowd = true;

    private double _revenue;
    private int _ticketsSold;

    public double Revenue => _revenue;
    public int TicketsSold => _ticketsSold;
    public float InflationMultiplier => Mathf.Pow(1.0f + _annualInflation, _yearsAhead);

    private void OnEnable()
    {
        _audience.MemberAdded += OnMemberAdded;
    }

    private void OnDisable()
    {
        _audience.MemberAdded -= OnMemberAdded;
    }

    private void OnMemberAdded(AudienceMember member, bool popIn)
    {
        if (!popIn && !_countStartingCrowd)
        {
            return;
        }

        var price = Random.Range(_ticketPriceRange.x, _ticketPriceRange.y) * InflationMultiplier;
        if (popIn)
        {
            price *= _walkUpMarkup;
        }
        _revenue += price;
        _ticketsSold++;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _audience = FindFirstObjectByType<AudienceSpawner>();
    }
#endif
}
