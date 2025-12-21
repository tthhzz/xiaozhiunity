using Cysharp.Threading.Tasks;

namespace XiaoZhi.Unity
{
    public class EmojiDisplay : IDisplay
    {
        private readonly Context _context;
        
        private WallpaperUI _wallpaperUI;

        private EmojiMainUI _emojiMainUI;
        
        public EmojiDisplay(Context context)
        {
            _context = context;
        }
        
        public void Dispose()
        {
            _wallpaperUI.Dispose();
            _emojiMainUI.Dispose();
        }
        
        public async UniTask<bool> Load()
        {
            _wallpaperUI = await _context.UIManager.ShowBgUI<WallpaperUI>();
            _emojiMainUI = await _context.UIManager.ShowSceneUI<EmojiMainUI>();
            return true;
        }

        public void Start()
        {
            
        }

        public async UniTask Show()
        {
            await _wallpaperUI.Show();
            await _emojiMainUI.Show();
        }

        public async UniTask Hide()
        {
            await _emojiMainUI.Hide();
            await _wallpaperUI.Hide();
        }

        public void Update(float deltaTime)
        {
        }
    }
}