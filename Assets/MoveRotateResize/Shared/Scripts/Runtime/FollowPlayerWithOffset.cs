//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.1
//------------------------------------------------------------------------------
using UnityEngine;
using MoveRotateResize.Dynamic.SystemsManager;
using System.Collections;

namespace MoveRotateResize.General
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class FollowPlayerWithOffset : MonoBehaviour
    {
        GameObject player;
        Transform playerTransform;
        Transform thisTransform;
        [SerializeField] private float heightOffset;

        // #if UNITY_EDITOR
        // private void OnValidate()
        // {
        //     playerTransform = DynamicSystemsManager.Instance.player?.transform;
        // }
        // #endif

        private void Awake()
        {
            thisTransform = transform;
        }

        private void Start()
        {        
            player = DynamicSystemsManager.player;
            playerTransform = player.transform;
            
            //Debug.Log($"GameManager initialized. PlayerController can now access its variables.");
        }

        private void Update()
        {
            if (playerTransform != null)
            {
                thisTransform.position = playerTransform.position + Vector3.up * heightOffset;
            }
        }
    }
}