using System;
using Cysharp.Threading.Tasks;

namespace XiaoZhi.Unity
{
    public interface IDisplay : IDisposable
    {
        UniTask<bool> Load();

        void Start();

        UniTask Show();
        
        UniTask Hide();

        void Update(float deltaTime);
    }
}