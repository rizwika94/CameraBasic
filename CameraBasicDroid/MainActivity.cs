using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Java.Lang;
using Java.Util.Concurrent;
using Android.Hardware.Camera2;
using Android.Content;
using Android.Media;
using Android.Util;
using CameraBasicDroid.Listener;
using Android.Hardware.Camera2.Params;
using Java.Text;
using Java.Util;
using System.Collections.Generic;
using Orientation = Android.Content.Res.Orientation;
using AndroidX.Core.App;


namespace CameraBasicDroid
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : Activity, ActivityCompat.IOnRequestPermissionsResultCallback
    {
        private AutoFitTextureView _textureView;
        public CameraDevice cameraDevice;
        private CameraManager manager;
        public CameraCaptureSession cameraCaptureSession;
        public CaptureRequest.Builder captureRequestBuilder;
        private ImageAvailableListener OnImageAvailableListener;
        public CameraCaptureListener captureCallback;
        private CameraStateListener _stateListener;
        public CaptureRequest previewRequest;
        private Size _imageDimension;
        private static readonly int MAX_PREVIEW_WIDTH = 1920;
        private static readonly int MAX_PREVIEW_HEIGHT = 1080;
        private string cameraId = "0"; // Rear camera is usually 0

        public Handler backgroundHandler;
        private HandlerThread _backgroundThread;
        private CaptureRequest.Builder stillCaptureBuilder;

        public Semaphore cameraOpenCloseLock = new Semaphore(1);
        private ImageReader imageReader;
        private int sensorOrientation;
        public const int STATE_PREVIEW = 0;

        public const int STATE_WAITING_LOCK = 1;

        public const int STATE_WAITING_PRECAPTURE = 2;

        public const int STATE_WAITING_NON_PRECAPTURE = 3;

        public const int STATE_PICTURE_TAKEN = 4;

        public int state = STATE_PREVIEW;

        // Whether the current camera device supports Flash or not.
        private bool isFlashSupported;
        private static readonly SparseIntArray ORIENTATIONS = new SparseIntArray();

        static readonly int REQUEST_CAMERA = 0;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.activity_main);

            _stateListener = new CameraStateListener(this);
            captureCallback = new CameraCaptureListener(this);

            _textureView = FindViewById<AutoFitTextureView>(Resource.Id.camera_preview);
            _textureView.SurfaceTextureListener = new TextureViewListener(this);

            Button captureButton = FindViewById<Button>(Resource.Id.button_capture);
            captureButton.Click += (sender, e) => TakePicture();
        }


        protected override void OnResume()
        {
            base.OnResume();
            OpenBackgroundThread();

            if (_textureView.IsAvailable)
            {
                OpenCamera(_textureView.Width, _textureView.Height);
            }
            else
            {
                _textureView.SurfaceTextureListener = new TextureViewListener(this);
            }
        }

        protected override void OnPause()
        {
            CloseCamera();
            CloseBackgroundThread();
            base.OnPause();
        }

        private void OpenBackgroundThread()
        {
            _backgroundThread = new HandlerThread("CameraBackground");
            _backgroundThread.Start();
            backgroundHandler = new Handler(_backgroundThread.Looper);
        }
        private void CloseBackgroundThread()
        {
            _backgroundThread?.QuitSafely();
            try
            {
                _backgroundThread?.Join();
                _backgroundThread = null;
                backgroundHandler = null;
            }
            catch (InterruptedException e)
            {
                e.PrintStackTrace();
            }
        }

        private static Size ChooseOptimalSize(Size[] choices, int textureViewWidth,
           int textureViewHeight, int maxWidth, int maxHeight, Size aspectRatio)
        {

            var bigEnough = new List<Size>();
            var notBigEnough = new List<Size>();
            int w = aspectRatio.Width;
            int h = aspectRatio.Height;

            for (var i = 0; i < choices.Length; i++)
            {
                Size option = choices[i];
                if ((option.Width <= maxWidth) && (option.Height <= maxHeight) &&
                       option.Height == option.Width * h / w)
                {
                    if (option.Width >= textureViewWidth &&
                        option.Height >= textureViewHeight)
                    {
                        bigEnough.Add(option);
                    }
                    else
                    {
                        notBigEnough.Add(option);
                    }
                }
            }
            if (bigEnough.Count > 0)
            {
                return (Size)Collections.Min(bigEnough, new CompareSizesByArea());
            }
            else if (notBigEnough.Count > 0)
            {
                return (Size)Collections.Max(notBigEnough, new CompareSizesByArea());
            }
            else
            {
                return choices[0];
            }
        }

        private void SetUpCameraOutputs(int width, int height)
        {

            var manager = (CameraManager)GetSystemService(Context.CameraService);
            try
            {
                for (var i = 0; i < manager.GetCameraIdList().Length; i++)
                {
                    var cameraIdFromList = manager.GetCameraIdList()[i];
                    CameraCharacteristics characteristics = manager.GetCameraCharacteristics(cameraIdFromList);

                    // We don't use a front facing camera in this sample.
                    var facing = (Integer)characteristics.Get(CameraCharacteristics.LensFacing);
                    if (facing != null && facing == (Integer.ValueOf((int)Android.Hardware.CameraFacing.Front)))
                    {
                        continue;
                    }

                    var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
                    if (map == null)
                    {
                        continue;
                    }

                    Size largest = (Size)Collections.Max(Arrays.AsList(map.GetOutputSizes((int)ImageFormatType.Jpeg)),
                        new CompareSizesByArea());
                    imageReader = ImageReader.NewInstance(largest.Width, largest.Height, ImageFormatType.Jpeg, /*maxImages*/2);
                    imageReader.SetOnImageAvailableListener(OnImageAvailableListener, backgroundHandler);


                    var displayRotation = WindowManager.DefaultDisplay.Rotation;
                    sensorOrientation = (int)characteristics.Get(CameraCharacteristics.SensorOrientation);
                    bool swappedDimensions = false;
                    switch (displayRotation)
                    {
                        case SurfaceOrientation.Rotation0:
                        case SurfaceOrientation.Rotation180:
                            if (sensorOrientation == 90 || sensorOrientation == 270)
                            {
                                swappedDimensions = true;
                            }
                            break;
                        case SurfaceOrientation.Rotation90:
                        case SurfaceOrientation.Rotation270:
                            if (sensorOrientation == 0 || sensorOrientation == 180)
                            {
                                swappedDimensions = true;
                            }
                            break;
                        default:
                            break;
                    }

                    Point displaySize = new Point();
                    WindowManager.DefaultDisplay.GetSize(displaySize);
                    var rotatedPreviewWidth = width;
                    var rotatedPreviewHeight = height;
                    var maxPreviewWidth = displaySize.X;
                    var maxPreviewHeight = displaySize.Y;

                    if (swappedDimensions)
                    {
                        rotatedPreviewWidth = height;
                        rotatedPreviewHeight = width;
                        maxPreviewWidth = displaySize.Y;
                        maxPreviewHeight = displaySize.X;
                    }

                    if (maxPreviewWidth > MAX_PREVIEW_WIDTH)
                    {
                        maxPreviewWidth = MAX_PREVIEW_WIDTH;
                    }

                    if (maxPreviewHeight > MAX_PREVIEW_HEIGHT)
                    {
                        maxPreviewHeight = MAX_PREVIEW_HEIGHT;
                    }

                    _imageDimension = ChooseOptimalSize(map.GetOutputSizes(Class.FromType(typeof(SurfaceTexture))),
                        rotatedPreviewWidth, rotatedPreviewHeight, maxPreviewWidth,
                        maxPreviewHeight, largest);

                    var orientation = Resources.Configuration.Orientation;
                    if (orientation == Orientation.Landscape)
                    {
                        _textureView.SetAspectRatio(_imageDimension.Width, _imageDimension.Height);
                    }
                    else
                    {
                        _textureView.SetAspectRatio(_imageDimension.Height, _imageDimension.Width);
                    }

                    // Check if the flash is supported.
                    var available = (Java.Lang.Boolean)characteristics.Get(CameraCharacteristics.FlashInfoAvailable);
                    if (available == null)
                    {
                        isFlashSupported = false;
                    }
                    else
                    {
                        isFlashSupported = (bool)available;
                    }

                    cameraId = cameraIdFromList;
                    return;
                }
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
            catch (NullPointerException e)
            {
                e.PrintStackTrace();
            }
        }


        public void OpenCamera(int width, int height)
        {
            if (ActivityCompat.CheckSelfPermission(this, Android.Manifest.Permission.Camera) != (int)Android.Content.PM.Permission.Granted)
            {

                ActivityCompat.RequestPermissions(this, new string[] { Android.Manifest.Permission.Camera }, REQUEST_CAMERA);
                return;
            }
            SetUpCameraOutputs(width, height);

            CameraManager manager = (CameraManager)GetSystemService(CameraService);
            try
            {
                if (!cameraOpenCloseLock.TryAcquire(2500, TimeUnit.Milliseconds))
                {
                    throw new RuntimeException("Time out waiting to lock camera opening.");
                }
                manager.OpenCamera(cameraId, _stateListener, backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
            catch (InterruptedException e)
            {
                throw new RuntimeException("Interrupted while trying to lock camera opening.", e);
            }
        }

        public void RunPrecaptureSequence()
        {
            try
            {
                captureRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, (int)ControlAEPrecaptureTrigger.Start);
                state = STATE_WAITING_PRECAPTURE;
                cameraCaptureSession.Capture(captureRequestBuilder.Build(), captureCallback, backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }



        public void CaptureStillPicture()
        {
            try
            {

                if (null == cameraDevice)
                {
                    return;
                }
                if (stillCaptureBuilder == null)
                    stillCaptureBuilder = cameraDevice.CreateCaptureRequest(CameraTemplate.StillCapture);

                stillCaptureBuilder.AddTarget(imageReader.Surface);

                stillCaptureBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
                SetAutoFlash(stillCaptureBuilder);

                int rotation = (int)this.WindowManager.DefaultDisplay.Rotation;
                stillCaptureBuilder.Set(CaptureRequest.JpegOrientation, GetOrientation(rotation));

                cameraCaptureSession.StopRepeating();
                cameraCaptureSession.Capture(stillCaptureBuilder.Build(), new CameraCaptureStillPictureSessionCallback(this), null);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        public void CreateCameraPreviewSession()
        {
            try
            {
                SurfaceTexture texture = _textureView.SurfaceTexture;
                if (texture == null)
                {
                    throw new IllegalStateException("texture is null");
                }

                texture.SetDefaultBufferSize(_imageDimension.Width, _imageDimension.Height);

                Surface surface = new Surface(texture);

                captureRequestBuilder = cameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
                captureRequestBuilder.AddTarget(surface);

                var zoomGestureListener = new ZoomGestureListener(cameraCaptureSession, manager.GetCameraCharacteristics(cameraDevice.Id),captureRequestBuilder,captureCallback);
                var touchEventHandler = new TouchEventHandler(this, zoomGestureListener);

                _textureView.SetOnTouchListener(touchEventHandler);
              

                List<Surface> surfaces = new List<Surface>();
                surfaces.Add(surface);
                surfaces.Add(imageReader.Surface);
                cameraDevice.CreateCaptureSession(surfaces, new CameraCaptureSessionListener(this), null);

            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        public void UnlockFocus()
        {
            try
            {

                captureRequestBuilder.Set(CaptureRequest.ControlAfTrigger, (int)ControlAFTrigger.Cancel);
                SetAutoFlash(captureRequestBuilder);

                cameraCaptureSession.SetRepeatingRequest(captureRequestBuilder.Build(), captureCallback, backgroundHandler);

            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        private void TakePicture()
        {
            LockFocus();
        }


        private void LockFocus()
        {
            try
            {


                captureRequestBuilder.Set(CaptureRequest.ControlAfTrigger, (int)ControlAFTrigger.Start);

                state = STATE_WAITING_LOCK;
                cameraCaptureSession.Capture(captureRequestBuilder.Build(), captureCallback,
                        backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        private int GetOrientation(int rotation)
        {
            manager = (CameraManager)GetSystemService(CameraService);
            CameraCharacteristics characteristics = null;
            if (manager != null)
            {
                try
                {
                    characteristics = manager.GetCameraCharacteristics(cameraDevice.Id);
                }
                catch (CameraAccessException e)
                {
                    e.PrintStackTrace();
                }
            }

            int sensorOrientation = (int)characteristics?.Get(CameraCharacteristics.SensorOrientation);
            int deviceOrientation = ORIENTATIONS.Get(rotation);

            if (sensorOrientation == null)
                return deviceOrientation;

            int result = (sensorOrientation - deviceOrientation + 360) % 360;

            return result;
        }



        private Java.IO.File GetOutputFile()
        {
            // Create a file to save the image
            string path = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryPictures).AbsolutePath;
            string timestamp = new SimpleDateFormat("yyyyMMdd_HHmmss", Locale.Us).Format(new Date());
            string filename = "IMG_" + timestamp + ".jpg";
            Java.IO.File file = new Java.IO.File(path, filename);
            return file;
        }


        private void CloseCamera()
        {
            try
            {
                cameraOpenCloseLock.Acquire();
                cameraCaptureSession?.Close();
                cameraCaptureSession = null;
                cameraDevice?.Close();
                cameraDevice = null;
                imageReader?.Close();
                imageReader = null;
            }
            catch (InterruptedException e)
            {
                throw new RuntimeException("Interrupted while trying to lock camera closing.", e);
            }
            finally
            {
                cameraOpenCloseLock.Release();
            }
        }

        public void SetAutoFlash(CaptureRequest.Builder requestBuilder)
        {
            if (isFlashSupported)
            {
                requestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.OnAutoFlash);
            }
        }

    }
}