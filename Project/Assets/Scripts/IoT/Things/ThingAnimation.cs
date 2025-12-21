using System.Linq;
using Cysharp.Threading.Tasks;

namespace XiaoZhi.Unity.IoT
{
    public class ThingAnimation : Thing
    {
        public ThingAnimation() : base("Animation Controller", "角色动作控制器")
        {
        }

        public override async UniTask Load()
        {
            var animLib = AppPresets.Instance.GetAnimationLib();
            var labels = "动作标签, " + string.Join(" 或 ", animLib.Sets.SelectMany(i => i.Labels));
            _methods.AddMethod("Animate", "角色做动作",
                new ParameterList(new[]
                {
                    new Parameter<string>("label", labels)
                }),
                Animate);
            _properties.AddProperty("IsEnabled", "角色是否支持动作", IsEnabled);
            await base.Load();
        }

        private void Animate(ParameterList parameters)
        {
            (_context.App.GetDisplay() as VRMDisplay)?.Animate(parameters.GetValue<string>("label"));
        }
        
        private bool IsEnabled()
        {
            return _context.App.GetDisplay() is VRMDisplay;
        }
    }
}