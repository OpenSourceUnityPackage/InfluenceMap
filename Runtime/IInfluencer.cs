using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InfluenceMapPackage
{
    public interface IInfluencer
    {
        Vector2 GetInfluencePosition();
        float GetInfluenceRadius();
    }
}
