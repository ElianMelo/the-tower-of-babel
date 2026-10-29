//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.1
//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections.Generic;

namespace MoveRotateResize.Dynamic.ShaderSwapper
{
    public class ShaderPropertyGroup : ScriptableObject
    {
        public List<string> BaseColorMap= new List<string> { 
            "_BaseMap", "_MainTex", "_Albedo", "_BaseColorMap" 
        };
        public List<string> NormalMap = new List<string> { 
            "_Normals", "_BumpMap", "_NormalMap"
        };
        public List<string> MaskMap = new List<string> { 
            "_MaskMap", "_Mask"
        };
        public List<string> MetallicMap= new List<string> { 
            "_MetallicMap"
        };
        public List<string> AmbientOcclusionMap = new List<string> { 
            "_AOMap", "_OcclusionMap", "_Occlusion"
        };
        public List<string> SmoothnessMap = new List<string> { 
            "_SmoothnessMap", "_SpecGlossMap","_Roughness","_RoughnessMap" 
        };
        public List<string> DetailMaskMap= new List<string> { 
            "_DetailMask", "_DetailMap" 
        };
        
        public delegate void ShaderPropertyUpdated();
        public event ShaderPropertyUpdated OnShaderPropertyUpdated;

        private void OnValidate()
        {
            OnShaderPropertyUpdated?.Invoke();
        }
    }
}