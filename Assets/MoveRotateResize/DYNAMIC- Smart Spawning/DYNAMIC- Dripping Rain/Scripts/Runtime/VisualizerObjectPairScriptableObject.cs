using UnityEngine;

namespace MoveRotateResize.Dynamic.DrippingRain
{
    [CreateAssetMenu(fileName = "NewVisualizerObjectPair", menuName = "Dynamic/Visualizer Object Pair")]
    public class VisualizerObjectPairScriptableObject : ScriptableObject
    {
        public GameObject drippingRainEditModeVisualizer; 
        public GameObject vfxInstance;
    }
}
