using System.Linq;
using Cysharp.Threading.Tasks;

namespace XiaoZhi.Unity.IoT
{
    public class ThingDance : Thing
    {
        public ThingDance() : base("Dance Controller", "角色跳舞控制器")
        {
        }

        public override async UniTask Load()
        {
            var dances = AppPresets.Instance.Dances;
            var names = "舞蹈名称, " + string.Join(" 或 ", dances.Select(i => i.Name));
            _methods.AddMethod("Dance", "角色跳舞",
                new ParameterList(new[]
                {
                    new Parameter<string>("name", names)
                }),
                Dance);
            _properties.AddProperty("IsEnabled", "角色是否支持跳舞", IsEnabled);
            _properties.AddProperty("IsDancing", "角色是否跳舞", IsDancing);
            await base.Load();
        }
        
        private void Dance(ParameterList parameters)
        {
            _context.App.Dance(parameters.GetValue<string>("name")).Forget();
        }
        
        private bool IsEnabled()
        {
            return _context.App.GetDisplay() is VRMDisplay;
        }
        
        private bool IsDancing()
        {
            return _context.App.Talk.Stat == Talk.State.Dancing;
        }

    }
}