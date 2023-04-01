using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.IO;
using Java.Nio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CameraBasicDroid.Listener
{
    public class ImageAvailableListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        private readonly File _file;

        public ImageAvailableListener(File file)
        {
            _file = file;
        }

        public void OnImageAvailable(ImageReader reader)
        {
            Image image = null;
            try
            {
                image = reader.AcquireLatestImage();
                ByteBuffer buffer = image.GetPlanes()[0].Buffer;
                byte[] bytes = new byte[buffer.Capacity()];
                buffer.Get(bytes);
                Save(bytes);
            }
            catch (FileNotFoundException e)
            {
                e.PrintStackTrace();
            }
            catch (IOException e)
            {
                e.PrintStackTrace();
            }
            finally
            {
                image?.Close();
            }
        }

        private void Save(byte[] bytes)
        {
            OutputStream output = null;
            try
            {
                output = new FileOutputStream(_file);
                output.Write(bytes);
            }
            finally
            {
                output?.Close();
            }
        }
    }
}