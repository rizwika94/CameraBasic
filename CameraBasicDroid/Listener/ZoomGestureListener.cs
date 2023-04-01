using Android.Hardware.Camera2;
using Android.Util;
using Android.Views;
using System;


namespace CameraBasicDroid.Listener
{
    public class ZoomGestureListener : ScaleGestureDetector.SimpleOnScaleGestureListener
    {
        private const float ZoomFactor = 0.05f;
        private float _zoomLevel = 1.0f;
        private readonly CameraCaptureSession _captureSession;
        private readonly CameraCharacteristics _cameraCharacteristics;
        private readonly CaptureRequest.Builder _captureRequestBuilder;
        private readonly CameraCaptureListener _cameraCaptureCallback;

        public ZoomGestureListener(CameraCaptureSession captureSession, CameraCharacteristics cameraCharacteristics, CaptureRequest.Builder captureRequestBuilder, CameraCaptureListener cameraCaptureListener)
        {
            _captureSession = captureSession;
            _cameraCharacteristics = cameraCharacteristics;
            _captureRequestBuilder = captureRequestBuilder;
            _cameraCaptureCallback = cameraCaptureListener;
        }

        public float ZoomLevel
        {
            get { return _zoomLevel; }
            set
            {
                _zoomLevel = value;
                _captureRequestBuilder.Set(CaptureRequest.ControlZoomRatio, GetZoomRatio());
                _captureSession.SetRepeatingRequest(_captureRequestBuilder.Build(), null, null);
            }
        }

        public override bool OnScale(ScaleGestureDetector detector)
        {
            _zoomLevel *= detector.ScaleFactor;
            _zoomLevel = Java.Lang.Math.Max(1f, Java.Lang.Math.Min(_zoomLevel, (float)_cameraCharacteristics.Get(CameraCharacteristics.ScalerAvailableMaxDigitalZoom)));
            ZoomLevel = ZoomLevel.Clamp(1.0f, 10);
            var zoomRatio = GetZoomRatio();
            try
            {
                _captureRequestBuilder.Set(CaptureRequest.ControlZoomRatio, zoomRatio);
                _captureSession.SetRepeatingRequest(_captureRequestBuilder.Build(), _cameraCaptureCallback, null);
            }
            catch (CameraAccessException ex)
            {
                Log.WriteLine(LogPriority.Error, "Camera2Sample", $"Failed to set zoom ratio: {ex.Message}");
            }

            return true;
        }

        private float GetZoomRatio()
        {
            var maxZoom = (float)_cameraCharacteristics.Get(CameraCharacteristics.ScalerAvailableMaxDigitalZoom);
            var minZoom = 1.0f;
            var zoom = _zoomLevel.Clamp(minZoom, maxZoom);
            return (float)Math.Pow(zoom, ZoomFactor);
        }
    }
}

public static class ExtensionMethods
{
    public static float Clamp(this float value, float min, float max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}