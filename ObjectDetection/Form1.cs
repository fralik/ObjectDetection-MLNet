using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using OpenCvSharp;
using Microsoft.ML;
using ObjectDetection.YoloParser;
using ObjectDetection.DataStructures;

namespace ObjectDetection
{
    public partial class Form1 : Form
    {
        private VideoCapture _capture;
        private bool _isRunning;
        private Image _mySharpImage;

        private string assetsRelativePath = @"../../../assets";
        private string assetsPath;
        private string modelFilePath;

        public Form1()
        {
            InitializeComponent();

            _capture = new VideoCapture(0);
            _isRunning = false;

            assetsPath = GetAbsolutePath(assetsRelativePath);
            modelFilePath = Path.Combine(assetsPath, "Model", "TinyYolo2_model.onnx");

            //Mat image = new Mat();
            //_capture.Read(image);
            //Console.WriteLine($"image size (height; width) = ({image.Height}; {image.Width})");
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _isRunning = false;
            // Uncomment if you want to clear output upon stop
            //pictureBox1.Image = null;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            // Frame image buffer
            Mat image = new Mat();
            _isRunning = true;
            btnStart.Enabled = false;

            var mlContext = new MLContext();

            // Create instance of model scorer
            var modelScorer = new OnnxModelScorer(modelFilePath, mlContext);
            // Load model only once
            var model = modelScorer.LoadModel();

            while (_isRunning)
            {
                _capture.Read(image); // read frame from webcam

                if (image.Empty())
                    break;

                // Store frame as in-memory source for ML.NET
                ImageNetData[] inMemoryCollection = new ImageNetData[]
                {
                    new ImageNetData
                    {
                        InputImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image),
                        Label = "",
                    }
                };
                var imageDataView = mlContext.Data.LoadFromEnumerable<ImageNetData>(inMemoryCollection);

                // Make another copy of the frame. We will use it to draw bounding boxes on it
                _mySharpImage = (Image)OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image);

                // Use model to score data
                IEnumerable<float[]> probabilities = modelScorer.Score(model, imageDataView);

                // Post-process model output
                YoloOutputParser parser = new YoloOutputParser();

                var boundingBoxes =
                    probabilities
                    .Select(probability => parser.ParseOutputs(probability))
                    .Select(boxes => parser.FilterBoundingBoxes(boxes, 5, .5F));
                // Since we only have a single frame, it is OK to have i = 0. Otherwise we would need
                // to iterate through images.
                var i = 0;
                IList<YoloBoundingBox> detectedObjects = boundingBoxes.ElementAt(i);
                DrawBoundingBox(ref _mySharpImage, detectedObjects);

                pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
                pictureBox1.Image = _mySharpImage;
                Cv2.WaitKey(1);

                _mySharpImage.Dispose();
                inMemoryCollection[0].InputImage.Dispose();
            }
            btnStart.Enabled = true;
        }
        public static string GetAbsolutePath(string relativePath)
        {
            FileInfo _dataRoot = new FileInfo(typeof(Program).Assembly.Location);
            string assemblyFolderPath = _dataRoot.Directory.FullName;

            string fullPath = Path.Combine(assemblyFolderPath, relativePath);

            return fullPath;
        }
        private static void LogDetectedObjects(IList<YoloBoundingBox> boundingBoxes)
        {
            if (boundingBoxes.Count == 0)
            {
                return;
            }

            Console.WriteLine($".....The objects in the image are detected as below....");

            foreach (var box in boundingBoxes)
            {
                Console.WriteLine($"{box.Label} and its Confidence score: {box.Confidence}");
            }

            Console.WriteLine("");
        }

        private static void DrawBoundingBox(ref Image image, IList<YoloBoundingBox> filteredBoundingBoxes)
        {
            var originalImageHeight = image.Height;
            var originalImageWidth = image.Width;

            foreach (var box in filteredBoundingBoxes)
            {
                // Get Bounding Box Dimensions
                var x = (uint)Math.Max(box.Dimensions.X, 0);
                var y = (uint)Math.Max(box.Dimensions.Y, 0);
                var width = (uint)Math.Min(originalImageWidth - x, box.Dimensions.Width);
                var height = (uint)Math.Min(originalImageHeight - y, box.Dimensions.Height);

                // Resize To Image
                x = (uint)originalImageWidth * x / OnnxModelScorer.ImageNetSettings.imageWidth;
                y = (uint)originalImageHeight * y / OnnxModelScorer.ImageNetSettings.imageHeight;
                width = (uint)originalImageWidth * width / OnnxModelScorer.ImageNetSettings.imageWidth;
                height = (uint)originalImageHeight * height / OnnxModelScorer.ImageNetSettings.imageHeight;

                // Bounding Box Text
                string text = $"{box.Label} ({(box.Confidence * 100).ToString("0")}%)";

                using (Graphics thumbnailGraphic = Graphics.FromImage(image))
                {
                    thumbnailGraphic.CompositingQuality = CompositingQuality.HighQuality;
                    thumbnailGraphic.SmoothingMode = SmoothingMode.HighQuality;
                    thumbnailGraphic.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    // Define Text Options
                    Font drawFont = new Font("Arial", 12, FontStyle.Bold);
                    SizeF size = thumbnailGraphic.MeasureString(text, drawFont);
                    SolidBrush fontBrush = new SolidBrush(Color.Black);
                    System.Drawing.Point atPoint = new System.Drawing.Point((int)x, (int)y - (int)size.Height - 1);

                    // Define BoundingBox options
                    Pen pen = new Pen(box.BoxColor, 3.2f);
                    SolidBrush colorBrush = new SolidBrush(box.BoxColor);

                    // Draw text on image 
                    thumbnailGraphic.FillRectangle(colorBrush, (int)x, (int)(y - size.Height - 1), (int)size.Width, (int)size.Height);
                    thumbnailGraphic.DrawString(text, drawFont, fontBrush, atPoint);

                    // Draw bounding box on image
                    thumbnailGraphic.DrawRectangle(pen, x, y, width, height);
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _capture.Release();
        }
    }
}
