using InfluenceMapPackage;
using UnityEngine;

public class Unit : MonoBehaviour, IInfluencer
{
    public ETeam team = ETeam.Team1;
    private bool m_isSelected = false;

    #region MonoBehaviour

    private void OnEnable()
    {
        GameManager.Instance.RegisterUnit(team, this);
    }

    private void OnDisable()
    {
        if(gameObject.scene.isLoaded)
            GameManager.Instance.UnregisterUnit(team, this);
    }
    
    #endregion

    public Vector2 GetInfluencePosition()
    {
        Vector3 position = transform.position;
        return new Vector2(position.x, position.z);
    }

    public float GetInfluenceRadius()
    {
        return 10f;
    }
}
