
//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using System;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace MoveRotateResize.Dynamic.MultiTag
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent, DefaultExecutionOrder(-1000000), ExecuteInEditMode]
    public class DynamicMultiTag : MonoBehaviour
    {
        [SerializeField] Tag[] _tags = Array.Empty<Tag>();
        public bool audioAttached = false;


        void Awake()
        {
            audioAttached = false;
            gameObject.AddTags(_tags);
        }

        void OnEnable()
        {
            gameObject.AddTags(_tags);
        }


        void OnDestroy()
        {
            gameObject.RemoveTags(_tags);
        }
                
        // Check if an audio instance is attached to this object
        public bool HasAudioAttached()
        {
            return audioAttached;
        }

        // Set the flag indicating whether an audio instance is attached
        public void SetAudioAttached(bool attached)
        {
            audioAttached = attached;
        }

        // Check if this object has a specific tag
        public bool HasTag(Tag tag)
        {
            return Array.Exists(_tags, t => t == tag);
        }

        // Get all tags assigned to this object
        public Tag[] GetTags()
        {
            return _tags;
        }

        public void Add(Tag tag)
        {
            if (Application.isEditor)
            {
#if UNITY_EDITOR
                ArrayUtility.Add(ref _tags, tag);
#endif
            }
            else
            {
                _tags = _tags.Concat(new Tag[] { tag }).ToArray();
            }
        }

#if UNITY_EDITOR
        internal void Remove(Tag tag)
        {
            ArrayUtility.Remove(ref _tags, tag);
        }

        internal bool Contains(Tag tag)
        {
            return ArrayUtility.Contains(_tags, tag);
        }
#endif
    }
}