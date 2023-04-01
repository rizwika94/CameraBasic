using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using CameraBasicDroid.Listener;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CameraBasicDroid.Listener
{
    public class TextureViewListener : Java.Lang.Object, TextureView.ISurfaceTextureListener
    {
        private readonly MainActivity _mainActivity;

        public TextureViewListener(MainActivity mainActivity)
        {
            _mainActivity = mainActivity;
        }

        public void OnSurfaceTextureAvailable(SurfaceTexture surfaceTexture, int width, int height)
        {
            _mainActivity.OpenCamera(width,height);
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
        {
            return true;
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {
            // Configure transform here if needed
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {
            // Invoked every time there's a new Camera preview frame
        }
    }

}

public class TouchEventHandler : Java.Lang.Object, View.IOnTouchListener
{
    private readonly ScaleGestureDetector _scaleDetector;
    private readonly ZoomGestureListener _zoomGestureListener;

    public TouchEventHandler(Context context, ZoomGestureListener zoomGestureListener)
    {
        _zoomGestureListener = zoomGestureListener;
        _scaleDetector = new ScaleGestureDetector(context, zoomGestureListener);
    }

    public bool OnTouch(View v, MotionEvent e)
    {
        _scaleDetector.OnTouchEvent(e);
        return true;
    }
}