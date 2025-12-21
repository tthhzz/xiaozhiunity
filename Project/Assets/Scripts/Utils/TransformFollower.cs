using UnityEngine;

namespace XiaoZhi.Unity
{
    public class TransformFollower : MonoBehaviour
    {
        private const float ZoomSpeed = 4;
        
        [SerializeField] private Bounds _bounds;
        [SerializeField] [Range(0, 1)] private float _topGap;
        [SerializeField] [Range(0, 1)] private float _bottomGap;

        private Camera _camera;
        private float _zoomGap;
        private bool _isInit;

        private void Start()
        {
            _camera = Camera.main;
        }

        public void SetFollower(Camera cam)
        {
            _camera = cam;
        }

        public void SetZoomGap(float gap)
        {
            _zoomGap = gap;
        }

        private void LateUpdate()
        {
            if (!_camera) return;
            if (_camera.orthographic)
            {
                var cameraTrans = _camera.transform;
                cameraTrans.forward = -transform.forward;
                var bottomGap = _bottomGap + (_zoomGap < 0 ? _zoomGap : 0);
                var topGap = _topGap + (_zoomGap > 0 ? _zoomGap : 0);
                var trueSize = new Vector3(_bounds.size.x, _bounds.size.y * (1 + topGap + bottomGap), _bounds.size.z);
                var trueCenter = new Vector3(_bounds.center.x,
                    _bounds.center.y + _bounds.size.y * 0.5f * (topGap - bottomGap), _bounds.center.z);
                var targetPos = transform.position + trueCenter + transform.forward * trueSize.z;
                var targetSize = trueSize.y * 0.5f;
                targetPos = !_isInit ? targetPos : Vector3.Lerp(cameraTrans.position, targetPos, Time.deltaTime * ZoomSpeed);
                targetSize = !_isInit ? targetSize : Mathf.Lerp(_camera.orthographicSize, targetSize, Time.deltaTime * ZoomSpeed);
                cameraTrans.position = targetPos;
                _camera.orthographicSize = targetSize;
                _isInit = true;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + _bounds.center, _bounds.size);
        }
    }
}