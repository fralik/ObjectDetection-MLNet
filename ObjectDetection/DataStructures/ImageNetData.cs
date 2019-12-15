using System.Drawing;
using Microsoft.ML.Transforms.Image;

namespace ObjectDetection.DataStructures
{
    public class ImageNetData
    {
        // Dimensions provided here seem not to play an important role
        [ImageType(480, 640)]
        public Bitmap InputImage { get; set; }

        public string Label { get; set; }

        public ImageNetData() 
        {
            InputImage = null;
            Label = "";
        }
    }
}
