using System;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public class AnimatorProxy : StateMachineBehaviour
    {
        public event Action<AnimatorStateInfo> StateUpdate;
        
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            StateUpdate?.Invoke(stateInfo);
        }
    }
}