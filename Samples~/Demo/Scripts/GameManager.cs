using System;
using System.Collections.Generic;
using InfluenceMapPackage;
using UnityEngine;
using UnityEngine.Rendering;

public enum ETeam : int
{
    Team1 = 0,
    Team2 = 1,
    [InspectorName(null)] TeamCount = 2
}

public class GameManager : MonoBehaviour
{
    public Camera m_camera;
    private List<Unit>[] m_teamsUnits = new List<Unit>[(int) ETeam.TeamCount];
    private bool m_isSelecting;

    public Terrain[] terrains;
    public TerrainInfluenceMap[] InfluenceMapTeam1;
    public TerrainInfluenceMap[] InfluenceMapTeam2;

    private Selection m_selection = new Selection();

    private ComputeShader m_previousShader; // Allow to update merger
    public ComputeShader shader;
    private static readonly int s_shaderPropertyTexture = Shader.PropertyToID("_Texture");
    private static int s_kernelIndex;
    private MapMerger m_merger;

#if UNITY_EDITOR
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
        m_previousShader = shader;
        m_merger = new MapMerger(shader);

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
        // Selection

        if (Input.GetMouseButtonDown(0))
        {
            if (!m_isSelecting)
            {
                m_selection.OnSelectionBegin(Input.mousePosition);
                m_isSelecting = true;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            m_isSelecting = false;
        }

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
                    terrain.materialTemplate = new Material(Shader.Find("Debug/DebugInfluenceMap"));
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

        if (shader != m_previousShader)
        {
            m_merger.SetShader(shader);
            m_previousShader = shader;
        }

        if (m_drawDebug)
        {
            for (var index = 0; index < terrains.Length; index++)
            {
                RenderTexture texture = m_merger.Process(InfluenceMapTeam1[index].RenderTexture, InfluenceMapTeam2[index].RenderTexture);
                terrains[index].materialTemplate.SetTexture(s_shaderPropertyTexture, texture);
            }
        }
#endif
    }

    void OnGUI()
    {
        // Influence map
        Vector2[] stats = GetStats();

        GUILayout.BeginVertical("box");
        GUILayout.Label("Global");
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

        // Selection
        if (m_isSelecting)
        {
            m_selection.DrawGUI(Input.mousePosition);

            Vector2 selectionStats = GetSelectionStat(m_selection.GetFirstPos(), Input.mousePosition);

            GUILayout.BeginVertical("box");
            GUILayout.Label("Selection");

            GUILayout.BeginHorizontal("box");
            
            GUILayout.Label($"Red : {selectionStats.x * 100f}%");
            GUILayout.Label($"Green : {selectionStats.y * 100f}%");

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }
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

            float widthStep1 = width1 / (float) width;
            float heightStep1 = height1 / (float) height;
            float widthStep2 = width2 / (float) width;
            float heightStep2 = height2 / (float) height;

            Color[] colors1 = InfluenceMapTeam1[index].GetDatas();
            Color[] colors2 = InfluenceMapTeam2[index].GetDatas();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float i1 = colors1[(int) (x * widthStep1 + y * heightStep1 * width1)].r;
                    float i2 = colors2[(int) (x * widthStep2 + y * heightStep2 * width2)].r;

                    rst[index].x += (i1 >= i2) ? i1 : 0;
                    rst[index].y += (i2 > i1) ? i2 : 0;
                }
            }

            rst[index] /= width * height;
        }

        return rst;
    }

    Vector2 GetSelectionStat(Vector2 pos1, Vector2 pos2)
    {
        Vector2 rst = Vector2.zero;
        float pixelCount = 0;

        // transform mouse screen space to world space
        Vector3 mousePos1 = m_camera.ScreenToWorldPoint(pos1);
        Vector3 mousePos2 = m_camera.ScreenToWorldPoint(pos2);

        Vector2 minMousePos = new Vector2(Math.Min(mousePos1.x, mousePos2.x), Math.Min(mousePos1.z, mousePos2.z));
        Vector2 maxMousePos = new Vector2(Math.Max(mousePos1.x, mousePos2.x), Math.Max(mousePos1.z, mousePos2.z));
        Rect worldRect = Rect.MinMaxRect(minMousePos.x, minMousePos.y, maxMousePos.x, maxMousePos.y);

        for (var index = 0; index < terrains.Length; index++)
        {
            Color[] colors1 = InfluenceMapTeam1[index].GetDatasInWorld(worldRect);
            Color[] colors2 = InfluenceMapTeam2[index].GetDatasInWorld(worldRect);

            if (colors1 == null || colors2 == null || colors1.Length != colors2.Length)
                continue;

            for (int i = 0; i < colors1.Length; i++)
            {
                float i1 = colors1[i].r;
                float i2 = colors2[i].r;
                
                rst.x += (i1 >= i2) ? i1 : 0;
                rst.y += (i2 > i1) ? i2 : 0;
            }

            pixelCount += colors1.Length;
        }

        return pixelCount > 0 ? rst / pixelCount : Vector2.zero;
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