using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace InfluenceMapPackage
{
    [RequireComponent(typeof(Terrain))]
    public class TerrainInfluenceMap : MonoBehaviour
    {
        public ComputeShader shader;
        
        [SerializeField, OnRangeChangedCall(3, 14, "GenerateRenderTexture")]
        [Tooltip("2^resolution is the size of renderTexture used. 3 is size of 8, 4 is 16, 5 is 32...")]
        private int resolution = 8;
        
        public int Resolution
        {
            get
            {
                return TwoPowX(resolution);
            }
            set
            {
                resolution = value;
                GenerateRenderTexture();
            }
        }
        
        public Vector2 Position
        {
            get
            {
                return new Vector2(m_terrain.GetPosition().x, m_terrain.GetPosition().z);
            }
        }
        public bool lowPrescision = true;

        private List<IInfluencer> m_influencers = new List<IInfluencer>();
        private RenderTexture m_renderTexture;
        
        private ComputeBuffer m_Datas;
        private Terrain m_terrain;
        private int m_kernelIndex;

        static readonly ProfilerMarker s_PreparePerfMarker =
            new ProfilerMarker(ProfilerCategory.Render, "InfluenceMapManager.Update");

        private static readonly int s_shaderPropertyDataCount = Shader.PropertyToID("_DataCount");
        private static readonly int s_shaderPropertyTextureSize = Shader.PropertyToID("_TextureSize");
        private static readonly int s_shaderPropertyDatas = Shader.PropertyToID("_Datas");
        private static readonly int s_shaderPropertyTextureOut = Shader.PropertyToID("_TextureOut");
        
        private static readonly string s_cmdName = "Process influence map";
        

#if UNITY_EDITOR
        private static readonly int s_shaderPropertyTexture = Shader.PropertyToID("_Texture");

        [SerializeField] private bool m_drawDebug = false;

        private bool m_prevDrawDebug = false;

        private Material m_prevTerrainMaterial;
        private Material m_debugMaterial;
#endif

        public IInfluencer[] Influencers => m_influencers.ToArray();
        
        public RenderTexture RenderTexture { get => m_renderTexture; }

        #region MonoBehaviour
        private void Awake()
        {
            m_kernelIndex = shader.FindKernel("main");
            m_terrain = GetComponent<Terrain>(); ;
            Assert.IsFalse(m_terrain.terrainData.size.x != m_terrain.terrainData.size.z, "Terrain need to be squared to process disc as fast as possible");
        }

        void OnEnable()
        {
            GenerateRenderTexture();
        }

        private void OnDisable()
        {
            if (m_Datas != null)
            {
                m_Datas.Dispose();
                m_Datas = null;
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (m_influencers.Count == 0)
                return;
            
            UpdateBuffer();
            if (m_Datas == null)
                return;
            
            int resol = Resolution;
            
            using (s_PreparePerfMarker.Auto())
            {
                CommandBuffer commandBuffer = new CommandBuffer();
                commandBuffer.name = s_cmdName;
                commandBuffer.SetComputeIntParam(shader, s_shaderPropertyDataCount, m_Datas.count);
                commandBuffer.SetComputeIntParam(shader, s_shaderPropertyTextureSize, resol);
                commandBuffer.SetComputeBufferParam(shader, m_kernelIndex, s_shaderPropertyDatas, m_Datas);
                commandBuffer.SetComputeTextureParam(shader, m_kernelIndex, s_shaderPropertyTextureOut, m_renderTexture);
                commandBuffer.DispatchCompute(shader, m_kernelIndex, resol / 8, resol / 8, 1);
                Graphics.ExecuteCommandBuffer(commandBuffer);
            }

#if UNITY_EDITOR
            if (m_prevDrawDebug != m_drawDebug)
            {
                m_prevDrawDebug = m_drawDebug;

                if (m_drawDebug)
                {
                    if (m_debugMaterial == null)
                        m_debugMaterial = new Material(Shader.Find("Debug/DebugInfluenceMap"));

                    m_prevTerrainMaterial = m_terrain.materialTemplate;
                    m_terrain.materialTemplate = m_debugMaterial;
                    m_terrain.materialTemplate.SetTexture(s_shaderPropertyTexture, m_renderTexture);
                }
                else
                {
                    m_terrain.materialTemplate = m_prevTerrainMaterial;
                }
            }
#endif
        }
#endregion

        public Color[] GetDatas(Rect localRect)
        {
            Assert.IsFalse(localRect.xMin < 0 || localRect.yMin < 0 || localRect.xMin > m_renderTexture.width ||
                           localRect.yMin > m_renderTexture.height ||
                           localRect.width < 0 || localRect.height < 0 || localRect.width > m_renderTexture.width ||
                           localRect.height > m_renderTexture.height ||
                           (int) localRect.height * (int) localRect.width == 0);
            
            Texture2D texture = new Texture2D((int)localRect.width, (int)localRect.height, lowPrescision ? TextureFormat.R8 : TextureFormat.R16, false);

            RenderTexture.active = m_renderTexture;
                 
            // Read pixels
            localRect.y = m_renderTexture.height - localRect.y - localRect.height; // renderTexture is vertical flipped
            texture.ReadPixels(localRect, 0, 0);
            texture.Apply();
                 
            RenderTexture.active = null; // added to avoid errors
            return texture.GetPixels();
        }

        public Color[] GetDatas()
        {
            return GetDatas(new Rect(0, 0, m_renderTexture.width, m_renderTexture.height));
        }

        Rect RemapTerrainRectToLocalRect(in Rect worldRect)
        {
            Rect locRect = new Rect();
            locRect.position = worldRect.position - Position;
            locRect.size = worldRect.size / Resolution;
            return locRect;
        }
        
        public Color[] GetDatasInWorld(in Rect worldRect)
        {
            
            return GetDatas(RemapTerrainRectToLocalRect(worldRect));
        }

        private void GenerateRenderTexture()
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
                return;
#endif
            int resol = Resolution;
            m_renderTexture = new RenderTexture(resol, resol, 0,
                lowPrescision ? RenderTextureFormat.R8 : RenderTextureFormat.R16);
            m_renderTexture.enableRandomWrite = true;
            m_renderTexture.filterMode = FilterMode.Point;
            m_renderTexture.Create();
            
#if UNITY_EDITOR
            if (m_drawDebug)
                m_terrain.materialTemplate.SetTexture(s_shaderPropertyTexture, m_renderTexture);
#endif
        }
        
        public static Vector2 Remap01(Vector2 value, Vector2 pos, Vector2 size)
        {
            return (value - pos) / size;
        }

        public void UpdateBuffer()
        {
            List<Vector3> buffer = new List<Vector3>(m_influencers.Count);

            TerrainData terrainData = m_terrain.terrainData;
            Vector2 terrainSize = new Vector2(terrainData.size.x, terrainData.size.z);
            Vector2 terrainMinPos = Position;
            Vector2 terrainMaxPos = terrainMinPos + terrainSize;
            
            foreach (IInfluencer influencer in m_influencers)
            {
                Vector2 influencePos = influencer.GetInfluencePosition();
                float influenceRadius = influencer.GetInfluenceRadius();

                if (IsDiscInsideAABB(influencePos, influenceRadius, terrainMinPos, terrainMaxPos))
                {
                    Vector2 posInTerrainSpace = Remap01(influencePos, terrainMinPos, terrainSize);
                    influenceRadius /= terrainSize.x; // assuming terrain is squared
                    
                    buffer.Add(new Vector3(posInTerrainSpace.x, posInTerrainSpace.y,
                        influenceRadius * influenceRadius));
                }
            }
            
            UpdateComputeBuffer(buffer.Count);
            m_Datas?.SetData(buffer);
        }

        public void UpdateComputeBuffer(int size)
        {
            bool canClear = false;
            if (m_Datas != null)
            {
                if (m_Datas.count == size)
                    return;
                
                m_Datas.Dispose();
                m_Datas = null;
                canClear = true;
            }

            if (size > 0)
                m_Datas = new ComputeBuffer(size, 3 * 4);
            else if (canClear)
            {
                //Clear render texture
                GenerateRenderTexture();
            }
        }

        public void RegisterEntity(IInfluencer influencer)
        {
            m_influencers.Add(influencer);
        }

        public void UnregisterEntity(IInfluencer influencer)
        {
            m_influencers.Remove(influencer);
        }
        
        // From https://stackoverflow.com/questions/11196700/math-pow-taking-an-integer-value
        public static int TwoPowX(int power)
        {
            return 1 << power;
        }

        
        // From https://codereview.stackexchange.com/questions/145809/high-performance-branchless-intersection-testing-sphere-aabb-aabb-aabb
        static bool IsDiscInsideAABB(Vector2 discCenter, float discRadius, Vector2 rectMin, Vector2 rectMax)
        {
            float ex = Mathf.Max(rectMin.x - discCenter.x, 0f) + Mathf.Max(discCenter.x - rectMax.x, 0f);
            float ey = Mathf.Max(rectMin.y - discCenter.y, 0f) + Mathf.Max(discCenter.y - rectMax.y, 0f);
            
            return (ex < discRadius) && (ey < discRadius) && (ex * ex + ey * ey < discRadius * discRadius);
        }
    }
}