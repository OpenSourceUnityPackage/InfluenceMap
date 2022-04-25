using System;
using System.Collections.Generic;
using InfluenceMapPackage;
using UnityEngine;

public enum ETeam : int
{
    Team1 = 0,
    Team2 = 1,
    [InspectorName(null)] TeamCount = 2
}

public class GameManager : MonoBehaviour
{
    public Camera camera;
    private List<Unit>[] m_teamsUnits = new List<Unit>[(int) ETeam.TeamCount];
    private bool m_isSelecting;

    public Terrain[] terrains;
    public TerrainInfluenceMap[] InfluenceMapTeam1;
    public TerrainInfluenceMap[] InfluenceMapTeam2;

#if UNITY_EDITOR
    private static readonly int s_shaderPropertyInfluenceMap1 = Shader.PropertyToID("_InfluenceMap1");
    private static readonly int s_shaderPropertyInfluenceMap2 = Shader.PropertyToID("_InfluenceMap2");

    [SerializeField] private bool m_drawDebug = false;

    private bool m_prevDrawDebug = false;

    private Material[] m_prevTerrainMaterial;
    private Material m_debugMaterial;
#endif

    #region Singleton

    private static GameManager m_Instance = null;

    public static GameManager Instance
    {
        get
        {
            if (m_Instance == null)
            {
                m_Instance = FindObjectOfType<GameManager>();
                if (m_Instance == null)
                {
                    GameObject newObj = new GameObject("GameManager");
                    m_Instance = Instantiate(newObj).AddComponent<GameManager>();
                }
            }

            return m_Instance;
        }
    }

    #endregion

    #region MonoBehaviour

    private void Awake()
    {
        for (var index = 0; index < m_teamsUnits.Length; index++)
        {
            m_teamsUnits[index] = new List<Unit>();
        }

        terrains = FindObjectsOfType<Terrain>();
        m_prevTerrainMaterial = new Material[terrains.Length];
        InfluenceMapTeam1 = new TerrainInfluenceMap[terrains.Length];
        InfluenceMapTeam2 = new TerrainInfluenceMap[terrains.Length];

        for (int i = 0; i < terrains.Length; i++)
        {
            TerrainInfluenceMap[] influenceMaps = terrains[i].GetComponents<TerrainInfluenceMap>();
            InfluenceMapTeam1[i] = influenceMaps[0];
            InfluenceMapTeam2[i] = influenceMaps[1];
        }
    }


    private void Update()
    {
        // Influence map
#if UNITY_EDITOR
        if (m_prevDrawDebug != m_drawDebug)
        {
            m_prevDrawDebug = m_drawDebug;

            if (m_drawDebug)
            {
                for (var index = 0; index < terrains.Length; index++)
                {
                    var terrain = terrains[index];
                    m_prevTerrainMaterial[index] = terrain.materialTemplate;
                    terrain.materialTemplate = new Material(Shader.Find("InfluenceMapMerger"));
                    ;
                }
            }
            else
            {
                for (var index = 0; index < terrains.Length; index++)
                {
                    terrains[index].materialTemplate = m_prevTerrainMaterial[index];
                }
            }
        }

        if (m_drawDebug)
        {
            for (var index = 0; index < terrains.Length; index++)
            {
                terrains[index].materialTemplate.SetTexture(s_shaderPropertyInfluenceMap1,
                    InfluenceMapTeam1[index].RenderTexture);
                terrains[index].materialTemplate.SetTexture(s_shaderPropertyInfluenceMap2,
                    InfluenceMapTeam2[index].RenderTexture);
            }
        }
#endif
    }

    void OnGUI()
    {
        Vector2[] stats = GetStats();

        GUILayout.BeginVertical("box");
        for (var index = 0; index < stats.Length; index++)
        {
            var stat = stats[index];
            GUILayout.BeginHorizontal("box");

            GUILayout.Label(terrains[index].name);
            GUILayout.Label($"Red : {stat.x * 100f}%");
            GUILayout.Label($"Green : {stat.y * 100f}%");

            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }
    #endregion

    Vector2[] GetStats()
    {
        Vector2[] rst = new Vector2[terrains.Length];
        
        for (var index = 0; index < terrains.Length; index++)
        {
            // Influence map 1 and 2 can have different size
            int width1 = InfluenceMapTeam1[index].RenderTexture.width;
            int height1 = InfluenceMapTeam1[index].RenderTexture.height;
            int width2 = InfluenceMapTeam2[index].RenderTexture.width;
            int height2 = InfluenceMapTeam2[index].RenderTexture.height;
            int width = Math.Min(width1, width2);
            int height = Math.Min(height1, height2);

            float widthStep1 = width1 / (float)width;
            float heightStep1 = height1 / (float)height;
            float widthStep2 = width2 / (float)width;
            float heightStep2 = height2 / (float)height;
            
            Color[] colors1 = InfluenceMapTeam1[index].GetDatas();
            Color[] colors2 = InfluenceMapTeam2[index].GetDatas();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float i1 = colors1[(int)(x * widthStep1 + y * heightStep1 * width1)].r;
                    float i2 = colors2[(int)(x * widthStep2 + y * heightStep2 * width2)].r;
                    
                    rst[index].x += (i1 >= i2) ? i1 : 0;
                    rst[index].y += (i2 > i1) ? i2 : 0;
                }                
            }

            rst[index].y /= width * height;
            rst[index].x /= width * height;
        }

        return rst;
    }

    /// <summary>
    /// Need to be called in OnEnable
    /// </summary>
    /// <example>
    ///private void OnEnable()
    ///{
    ///    GameManager.Instance.RegisterUnit(team, this);
    ///}
    /// </example>
    /// <param name="team"></param>
    public void RegisterUnit(ETeam team, Unit unit)
    {
        m_teamsUnits[(int) team].Add(unit);

        switch (team)
        {
            case ETeam.Team1:
                foreach (TerrainInfluenceMap influenceMap in InfluenceMapTeam1)
                    influenceMap.RegisterEntity(unit);
                break;
            case ETeam.Team2:
                foreach (TerrainInfluenceMap influenceMap in InfluenceMapTeam2)
                    influenceMap.RegisterEntity(unit);
                break;
            case ETeam.TeamCount:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(team), team, null);
        }
    }

    /// <summary>
    /// Need to be called in OnDisable
    /// </summary>
    /// <example>
    ///private void OnDisable()
    ///{
    ///    if(gameObject.scene.isLoaded)
    ///        GameManager.Instance.UnregisterUnit(team, this);
    ///}
    /// </example>
    /// <param name="team"></param>
    public void UnregisterUnit(ETeam team, Unit unit)
    {
        m_teamsUnits[(int) team].Remove(unit);
        switch (team)
        {
            case ETeam.Team1:
                foreach (TerrainInfluenceMap influenceMap in InfluenceMapTeam1)
                    influenceMap.UnregisterEntity(unit);
                break;
            case ETeam.Team2:
                foreach (TerrainInfluenceMap influenceMap in InfluenceMapTeam2)
                    influenceMap.UnregisterEntity(unit);
                break;
            case ETeam.TeamCount:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(team), team, null);
        }
    }
}