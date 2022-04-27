using UnityEngine;
using UnityEngine.Rendering;

namespace InfluenceMapPackage
{
    public class MapMerger
    {
        private ComputeShader m_shader;
        private static readonly int s_shaderPropertyMap1 = Shader.PropertyToID("_Map1");
        private static readonly int s_shaderPropertyMap2 = Shader.PropertyToID("_Map2");
        private static readonly int s_shaderPropertyOutput = Shader.PropertyToID("_TextureOut");
        private int s_kernelIndex = 0;

        public MapMerger(ComputeShader shader)
        {
            SetShader(shader);
        }

        public void SetShader(ComputeShader shader)
        {
            m_shader = shader;
            s_kernelIndex = m_shader.FindKernel("main");
        }

        public RenderTexture Process(RenderTexture map1, RenderTexture map2)
        {
            if (map1.width != map1.height || map2.width != map2.height || map1.width != map2.height)
            {
                Debug.LogError("Maps need to be uniforms");
                return null;
            }

            int resol = map1.width;
            RenderTexture rst = RenderTexture.GetTemporary(resol, resol, 0, map1.format);
            rst.enableRandomWrite = true;
            rst.filterMode = FilterMode.Point;
            rst.Create();

            CommandBuffer commandBuffer = new CommandBuffer();
            commandBuffer.name = "Compute shader";
            commandBuffer.SetComputeTextureParam(m_shader, s_kernelIndex, s_shaderPropertyMap1, map1);
            commandBuffer.SetComputeTextureParam(m_shader, s_kernelIndex, s_shaderPropertyMap2, map2);
            commandBuffer.SetComputeTextureParam(m_shader, s_kernelIndex, s_shaderPropertyOutput, rst);
            commandBuffer.DispatchCompute(m_shader, s_kernelIndex, resol / 8, resol / 8, 1);
            Graphics.ExecuteCommandBuffer(commandBuffer);

            return rst;
        }
    }
}
