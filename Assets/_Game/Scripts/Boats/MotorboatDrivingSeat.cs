using UnityEngine;

// Visual-only placement: locate the authored port/left seat and steering wheel once.
// No physics, update loop, extra draw calls, or colliders on mobile.
[DisallowMultipleComponent]
public sealed class MotorboatDrivingSeat : MonoBehaviour
{
    private Vector3 seatLocal;
    private Vector3 wheelLocal;
    private float gripRadius;
    private bool ready;

    public static MotorboatDrivingSeat For(BoatController boat)
    {
        if (boat == null || !boat.IsMotorboat) return null;
        MotorboatDrivingSeat found = boat.GetComponent<MotorboatDrivingSeat>();
        return found != null ? found : boat.gameObject.AddComponent<MotorboatDrivingSeat>();
    }

    public Vector3 SeatSurface => transform.TransformPoint(seatLocal);
    public Vector3 LeftGrip => transform.TransformPoint(wheelLocal - Vector3.right * gripRadius);
    public Vector3 RightGrip => transform.TransformPoint(wheelLocal + Vector3.right * gripRadius);

    private void Awake() { Locate(); }

    private static bool Has(string name, string term) =>
        name.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0;

    private void Locate()
    {
        if (ready) return;
        ready = true;

        BoatController boat = GetComponent<BoatController>();
        if (boat == null || boat.DriverSeat == null) return;
        // Safe fallback if seat meshes were authored without descriptive names.
        seatLocal = transform.InverseTransformPoint(boat.DriverSeat.position);
        seatLocal.x = Mathf.Min(-.35f, seatLocal.x);
        seatLocal.y += .48f;

        Transform model = transform.Find("ModelContainer/ImportedMotorboat");
        if (model == null) return;
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        Renderer wheel = null;
        int wheelScore = -1;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            string n = r.name;
            string parent = r.transform.parent != null ? r.transform.parent.name : "";
            int score = Has(n, "steering") && Has(n, "wheel") ? 5 :
                        Has(n, "wheel") ? 4 : Has(parent, "steering") && Has(parent, "wheel") ? 3 : 0;
            if (score > wheelScore && score > 0) { wheel = r; wheelScore = score; }
        }
        if (wheel != null)
        {
            wheelLocal = transform.InverseTransformPoint(wheel.bounds.center);
            // Grip locations follow the real wheel centre as the boat banks.
            float width = transform.InverseTransformVector(wheel.bounds.size).magnitude;
            gripRadius = Mathf.Clamp(width * .2f, .13f, .27f);
        }
        else
        {
            wheelLocal = seatLocal + new Vector3(0f, .40f, .65f);
            gripRadius = .19f;
        }

        float best = float.NegativeInfinity;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            string n = r.name;
            string parent = r.transform.parent != null ? r.transform.parent.name : "";
            bool cushion = Has(n, "cushion") || Has(n, "upholstery") || Has(n, "squab");
            bool seat = cushion || Has(n, "seat") || Has(parent, "seat") || Has(n, "bolster");
            if (!seat) continue;

            Vector3 center = transform.InverseTransformPoint(r.bounds.center);
            float thickness = Mathf.Abs(transform.InverseTransformVector(r.bounds.size).y);
            // Find the LEFT seat behind the wheel, preferring the sitting cushion
            // over its high backrest or metal support.
            float score = (cushion ? 12f : 0f) + (Has(n, "seat") ? 4f : 0f)
                + (center.x < 0f ? 9f : -12f)
                - Mathf.Abs(center.x - wheelLocal.x) * 3f
                - Mathf.Abs(center.z - (wheelLocal.z - .60f)) * 5f
                - Mathf.Abs(center.y - (wheelLocal.y - .35f)) * 2f
                - Mathf.Max(0f, thickness - .35f) * 15f;
            if (score <= best) continue;
            best = score;
            Vector3 top = transform.InverseTransformPoint(
                new Vector3(r.bounds.center.x, r.bounds.max.y, r.bounds.center.z));
            seatLocal = top;
        }
        if (best == float.NegativeInfinity)
            Debug.LogWarning("Motorboat: no named seat cushion mesh was found. Using the left-side helm fallback; name the cushion Seat or Upholstery for exact placement.", this);
    }
}
