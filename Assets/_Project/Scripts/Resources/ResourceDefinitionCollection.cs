using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Resources
{
    [CreateAssetMenu(fileName = "ResourceDefinitionCollection", menuName = "Tower of Babel/Resources/Resource Definition Collection")]
    public sealed class ResourceDefinitionCollection : ScriptableObject
    {
        [SerializeField] private List<ResourceDefinition> definitions = new();

        public IReadOnlyList<ResourceDefinition> Definitions => definitions;
    }
}
