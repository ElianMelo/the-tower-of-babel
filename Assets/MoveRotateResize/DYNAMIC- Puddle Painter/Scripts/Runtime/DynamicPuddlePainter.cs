//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.2
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal; // Ported from HDRP: project uses URP.
using UnityEngine.VFX;

#if UNITY_EDITOR
namespace MoveRotateResize.Dynamic.PuddlePainter
{
    [ExecuteInEditMode]
    public class DynamicPuddlePainter : MonoBehaviour
    {
        public enum PaintChannel
        {
            ProceduralPuddleMask,
            Wetness,
            Puddles,
            ProceduralTerrainPuddleMask,
            AllLayers,
            AllLayersMinusTerrain
        }

        public LayerMask TerrainLayer;
        public RenderTexture puddlePaintingRenderTexture;
        public GameObject paintBrush;
        GameObject foundPaintBrush;
        public float brushSize = 10f; 
        public float decalSize = 1000f; 
        public float decalDepth = 300f;   
        public float cameraYOffset = 300f;    
        public PaintChannel paintChannel = PaintChannel.Puddles;
        [HideInInspector] public string folderPath;
        string directoryPath;

        public GameObject GetChildWithVFXComponent()
        {
            foreach (Transform child in transform)
            {
                VisualEffect vfxComponent = child.GetComponent<VisualEffect>();
                if (vfxComponent != null)
                {
                    //Debug.Log("Found VFX component in child: " + child.gameObject.name);
                    return child.gameObject;
                }
            }
            Debug.Log("Puddle Brush is not assigned.");
            return null;
        }
    
        void OnValidate() 
        {
            AdjustChildObjects();
        }

        void OnEnable()
        {
            AdjustChildObjects();
        }

        void Start()
        {
            GameObject foundPaintBrush = GetChildWithVFXComponent();
            if (foundPaintBrush != null)
            {
                //Debug.Log("Assigning paint brush: " + foundPaintBrush.name);
            }
            else
            {
                Debug.LogWarning("No paint brush found.");
            }

            paintBrush = foundPaintBrush;
            EditorUtility.SetDirty(this);


            if (Application.isPlaying)
            {
                Debug.Log("Application is playing. Disabling DynamicPuddlePainter.");
                enabled = false;
            }
            else
            {
                if (paintBrush != null)
                {
                    //Debug.Log("Disabling paint brush.");
                    paintBrush.SetActive(false);
                }
                else
                {
                    #if DEVELOPMENT_BUILD || UNITY_EDITOR
                    Debug.LogWarning("Puddle Brush is not assigned.");
                    #endif
                }                
            }
        }

        private void AdjustChildObjects()
        {
            foreach (Transform child in transform)
            {
                DecalProjector decal = child.GetComponent<DecalProjector>();
                if (decal != null)
                {
                    //Debug.Log("Adjusting DecalProjector size for child: " + child.gameObject.name);
                    decal.size = new Vector3(decalSize, decalSize, decalDepth);
                }
            }

            Camera camera = GetComponentInChildren<Camera>();
            if (camera != null)
            {
                //Debug.Log("Adjusting Camera settings.");
                camera.orthographicSize = decalSize / 2.0f;
                camera.transform.position = new Vector3(camera.transform.position.x, cameraYOffset, camera.transform.position.z);
            }
            else
            {
                //Debug.LogWarning("No Camera found in children.");
            }

            GameObject prefabAssetRoot = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (prefabAssetRoot != null)
            {
                directoryPath = AssetDatabase.GetAssetPath(prefabAssetRoot);
                folderPath = System.IO.Path.GetDirectoryName(directoryPath);
                //Debug.Log("Prefab asset found. Folder Path: " + folderPath);
            }
            else
            {
                //Debug.LogWarning("No prefab asset found for the GameObject.");
            }
        }
    }
}
#endif