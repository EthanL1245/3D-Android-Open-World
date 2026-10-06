using UnityEngine;

public sealed partial class SnapperIslandRockscape
{
    void Majors()
    {
        P(0, 5,-8,2.35f, 8, 28,-7,.22f,true,true); P(2, 1,-7,2.20f,-6,132, 9,.24f,true,true);
        P(1, 9,-5,2.05f,11,218,-5,.22f,true,true); P(4, 4,-13,1.95f,-9,302, 8,.25f,true,true);
        P(5,11,-11,1.75f, 6, 74,12,.23f,true,true);
        P(0,-18,-7,2.05f,-8,338, 5,.22f,true,true); P(3,-22,-4,1.85f, 7, 83,-9,.24f,true,true);
        P(1,-14,-10,1.75f,10,174, 6,.23f,true,true); P(5,-23,-11,1.55f,-5,252,11,.25f,false,true);
        P(2,19,-11,1.95f, 9, 42,-8,.23f,true,true); P(4,23,-7,1.80f,-7,139,10,.23f,true,true);
        P(1,16,-16,1.70f,11,231,-6,.24f,true,true); P(3,25,-14,1.55f,-9,319, 7,.25f,false,true);
        P(4,-15,11,1.55f, 7, 19,-7,.22f,true,true); P(1,-20,14,1.40f,-8,121, 9,.23f,false,true);
        P(5,18,10,1.45f,10,205,-5,.22f,true,true);
    }

    void Transitions()
    {
        Cluster(-31,1,10,8,8,1.05f,1.65f,true); Cluster(31,2,9,10,7,1f,1.60f,true);
        Cluster(-12,-25,12,8,7,.95f,1.55f,true); Cluster(12,18,10,7,5,.85f,1.35f,false);
        Cluster(-27,22,8,6,4,.85f,1.30f,false);
    }

    void Shore()
    {
        P(3,48,-8,1.85f,8,32,-6,.18f,true,true,.30f); P(5,51,-12,1.35f,-7,141,8,.18f,false,false,.43f);
        P(4,44,-16,1.55f,5,249,-9,.20f,true,true,.25f); P(1,49,1,1.20f,-9,314,6,.18f,false,false,.38f);
        P(5,-48,4,1.75f,-6,77,10,.20f,true,true,.27f); P(3,-51,-1,1.35f,9,188,-7,.18f,false,false,.46f);
        P(1,-45,-10,1.55f,-8,268,5,.20f,true,true,.24f); P(4,-49,12,1.15f,6,347,-8,.18f,false,false,.36f);
        P(0,-35,-29,1.85f,10,22,-8,.22f,true,true,.20f); P(5,-40,-31,1.25f,-7,129,9,.18f,false,false,.44f);
        P(3,-30,-34,1.20f,8,238,-6,.18f,false,false,.35f);
        P(4,34,-30,1.80f,-7,56,8,.21f,true,true,.22f); P(5,40,-28,1.35f,9,167,-8,.18f,false,false,.42f);
        P(1,29,-35,1.20f,-8,287,7,.18f,false,false,.34f);
        P(3,37,25,1.55f,6,91,-7,.20f,true,true,.22f); P(5,42,23,1.10f,-7,214,8,.18f,false,false,.41f);
    }
}
