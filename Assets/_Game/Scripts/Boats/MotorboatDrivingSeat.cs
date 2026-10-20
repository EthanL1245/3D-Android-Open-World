using UnityEngine;

// Visual-only placement: locate the authored port/left seat and steering wheel once.
// No physics, update loop, extra draw calls, or colliders on mobile.
[DisallowMultipleComponent]
public sealed class MotorboatDrivingSeat : MonoBehaviour
{
    private Vector3 seatLocal;
    private Vector3 wheelLocal;
    private float gripRadius;
    private Transform wheelTransform;
    private Bounds wheelMeshBounds;
    private Vector3 passengerLocal;
    private Transform passengerAnchor;
    public Transform PassengerAnchor => passengerAnchor;
    public Vector3 PassengerSurface => transform.TransformPoint(passengerLocal);
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

    // Closest point on the outside ring keeps the whole-body adjustment small,
    // instead of forcing an authored hand pose to an arbitrary clock position.
    public Vector3 ClosestRimPoint(Vector3 palm)
    {
        if (wheelTransform == null) return RightGrip;
        Vector3 e = wheelMeshBounds.extents;
        int normal = e.x < e.y && e.x < e.z ? 0 : e.y < e.z ? 1 : 2;
        int u = (normal+1)%3, v = (normal+2)%3;
        Vector3 local = wheelTransform.InverseTransformPoint(palm)-wheelMeshBounds.center;
        float a = local[u]/Mathf.Max(.0001f,e[u]);
        float b = local[v]/Mathf.Max(.0001f,e[v]);
        float angle = Mathf.Atan2(b,a);
        Vector3 rim = wheelMeshBounds.center;
        rim[u] += Mathf.Cos(angle)*e[u];
        rim[v] += Mathf.Sin(angle)*e[v];
        return wheelTransform.TransformPoint(rim);
    }

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
        passengerLocal = new Vector3(-seatLocal.x, seatLocal.y, seatLocal.z);
        passengerAnchor = new GameObject("Motorboat Right Passenger Anchor").transform;
        passengerAnchor.SetParent(transform, false);
        passengerAnchor.localPosition = passengerLocal;

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
            wheelTransform = wheel.transform;
            wheelMeshBounds = wheel.localBounds;
            Bounds wheelBounds = BoatLocalBounds(wheel);
            wheelLocal = wheelBounds.center;
            // Grip locations follow the real wheel centre as the boat banks.
            // The outer rim, not the hub/spokes or a diagonal of world bounds.
            gripRadius = wheelBounds.extents.x;
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

            Bounds bounds = BoatLocalBounds(r);
            Vector3 center = bounds.center;
            float thickness = bounds.size.y;
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
            Vector3 top = new Vector3(center.x, bounds.max.y, center.z);
            seatLocal = top;
        }
        // Use the matching cushion on the right to obtain the lateral spacing.
        // Keep the driver's existing height and fore/aft position exactly.
        float rightScore = float.NegativeInfinity;
        passengerLocal = new Vector3(-seatLocal.x, seatLocal.y, seatLocal.z);
        foreach (Renderer r in renderers)
        {
            if (!Has(r.name, "cushion")) continue;
            Bounds bounds = BoatLocalBounds(r);
            if (bounds.center.x <= 0f) continue;
            float score = -Mathf.Abs(bounds.center.z-seatLocal.z) - Mathf.Abs(bounds.max.y-seatLocal.y);
            if (score <= rightScore) continue;
            rightScore = score;
            passengerLocal.x = bounds.center.x;
        }
        passengerAnchor.localPosition = passengerLocal;
        if (best == float.NegativeInfinity)
            Debug.LogWarning("Motorboat: no named seat cushion mesh was found. Using the left-side helm fallback; name the cushion Seat or Upholstery for exact placement.", this);
    }
    private Bounds BoatLocalBounds(Renderer renderer)
    {
        Bounds local = renderer.localBounds;
        Matrix4x4 toBoat = transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
        Bounds result = new Bounds(toBoat.MultiplyPoint3x4(local.center), Vector3.zero);
        for (int x=-1; x<=1; x+=2)
            for (int y=-1; y<=1; y+=2)
                for (int z=-1; z<=1; z+=2)
                    result.Encapsulate(toBoat.MultiplyPoint3x4(local.center +
                        Vector3.Scale(local.extents, new Vector3(x,y,z))));
        return result;
    }

}
