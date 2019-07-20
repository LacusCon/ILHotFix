using DragonBones;
using Transform = UnityEngine.Transform;

namespace LC_Tools
{
    public class DragonBonesForIL
    {
        private UnityArmatureComponent _armature;
        public void InitComponent(Transform baseTF)
        {
            _armature = baseTF.GetComponent<UnityArmatureComponent>();
        }

        public void Play(string animation, int times)
        {
            _armature.animation.Play(animation, times);
        }
    }
}