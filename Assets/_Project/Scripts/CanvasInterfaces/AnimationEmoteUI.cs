using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel
{
    public class AnimationEmoteUI : MonoBehaviour
    {
        [SerializeField] private List<OptionButton> options = new();

        public void Show()
        {
            foreach (var option in options)
            {
                option.ToggleHover(false);
            }
        }

        public void Hide()
        {
            foreach (var option in options)
            {
                option.ToggleHover(false);
            }
        }
    }
}
