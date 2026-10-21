#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using TowerOfBabel.Players;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class FrutigirGirlPlayModeTests
    {
        [UnityTest]
        public IEnumerator EmoteAndNetworkAnimationUseJumpAndReturnToLocomotion()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/FrutigirGirl.prefab");
            var local=Object.Instantiate(prefab); var remote=Object.Instantiate(prefab);
            try
            {
                yield return null;
                var visuals=local.GetComponent<PlayerVisuals>(); var replicated=remote.GetComponent<PlayerVisuals>();
                var animator=local.GetComponent<Animator>(); var remoteAnimator=remote.GetComponent<Animator>();
                visuals.PlayEmote(PlayerAnimationTrigger.Dancing);
                animator.Update(0); animator.Update(.2f); yield return null;
                Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip.name,Is.EqualTo("Jump"));
                replicated.ApplyNetworkAnimation(PlayerAnimationState.Dance,visuals.LatestTrigger,visuals.TriggerSequence);
                remoteAnimator.Update(0); remoteAnimator.Update(.2f); yield return null;
                Assert.That(remoteAnimator.GetCurrentAnimatorClipInfo(0)[0].clip.name,Is.EqualTo("Jump"));
                visuals.SetMovement(true,false,true); yield return null;
                yield return null;
                animator.Update(.2f);
                Assert.That(visuals.CurrentAnimationState,Is.EqualTo(PlayerAnimationState.Walk));
                foreach(var skin in local.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var baked=new Mesh(); skin.BakeMesh(baked);
                    Assert.That(float.IsNaN(baked.bounds.size.x),Is.False,skin.name);
                    Assert.That(baked.vertexCount,Is.GreaterThan(0)); Object.Destroy(baked);
                }
            }
            finally { Object.Destroy(local); Object.Destroy(remote); }
            yield return null;
        }
    }
}
#endif
