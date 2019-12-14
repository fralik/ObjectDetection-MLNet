# Realtime Object Detection from a webcam

| ML.NET version | API type          | Status                        | App Type    | Data type | Scenario            | ML Task                   | Algorithms                  |
|----------------|-------------------|-------------------------------|-------------|-----------|---------------------|---------------------------|-----------------------------|
| v1.4           | Dynamic API | Up-to-date | GUI app | in-memory | Object Detection | Deep Learning  | Tiny Yolo2 ONNX model |


This is an extension of [Object Detection](https://github.com/dotnet/machinelearning-samples/tree/master/samples/csharp/getting-started/DeepLearning_ObjectDetection_Onnx) sample from Microsoft.
Instead of loading prediction data from disc, this application uses images from a webcam. Other than that it is the same application.

For a detailed explanation of how to build original Object Detection application, see the accompanying [tutorial](https://docs.microsoft.com/en-us/dotnet/machine-learning/tutorials/object-detection-onnx) on the Microsoft Docs site. 

## System Requirements

Make sure that [Git LSF](https://git-lfs.github.com/) is installed on your system **before you clone** this repository.

Apart from obvious .NET Core >= 3.1 and ML.NET library this application depends on [OpenCvSharp4](https://github.com/shimat/opencvsharp)
and [OpenCV](https://opencv.org/) libraries.

OpenCvSharp4 will be installed automatically as NuGet package.

You will need to install OpenCV binaries manually. If you are using Windows, then you can download pre-built binaries. If you are
on macOS or Linux, then you will need to build OpenCV yourself. Refer to one of the tutorials out there on how to build OpenCV,
one example for macOS is here https://www.pyimagesearch.com/2018/08/17/install-opencv-4-on-macos/.

Once you have OpenCV binaries you have to put them alongside ObjectDetection executable. 
In Windows with pre-built binaries you can do it like this:

1. Extract downloaded arcive.

2. Copy all files from `opencv/build/x64/vc15/bin` to the deepest folder under `bin` folder in Object Detection project, i.e. `ObjectDetection\ObjectDetection\bin\Debug\netcoreapp3.1`.

These instructions were used with ML.NET 1.4, Visual Studio 2019, and OpenCV 4.1.2.

## Problem 
Object detection is one of the classical problems in computer vision: Recognize what the objects are inside a given image and also where they are in the image. For these cases, you can either use pre-trained models or train your own model to classify images specific to your custom domain. 
 
## DataSet
There is no dataset involved. Live images will be used.

## Pre-trained model
There are multiple models which are pre-trained for identifying multiple objects in the images. Here we are using the pretrained model, **Tiny Yolo2** in  **ONNX** format. This model is a real-time neural network for object detection that detects 20 different classes. It is made up of 9 convolutional layers and 6 max-pooling layers and is a smaller version of the more complex full [YOLOv2](https://pjreddie.com/darknet/yolov2/) network.

The Open Neural Network eXchange i.e [ONNX](http://onnx.ai/) is an open format to represent deep learning models. With ONNX, developers can move models between state-of-the-art tools and choose the combination that is best for them. ONNX is developed and supported by a community of partners.

The model is downloaded from the [ONNX Model Zoo](https://github.com/onnx/models/tree/master/tiny_yolov2) which is a is a collection of pre-trained, state-of-the-art models in the ONNX format.

The Tiny YOLO2 model was trained on the [Pascal VOC](http://host.robots.ox.ac.uk/pascal/VOC/) dataset. Below are the model's prerequisites. 

**Model input and output**

**Input**

Input image of the shape (3x416x416)  

**Output**

Output is a (1x125x13x13) array   

**Pre-processing steps**

Resize the input image to a (3x416x416) array of type float32.

**Post-processing steps**

The output is a (125x13x13) tensor where 13x13 is the number of grid cells that the image gets divided into. Each grid cell corresponds to 125 channels, made up of the 5 bounding boxes predicted by the grid cell and the 25 data elements that describe each bounding box (5x25=125). For more information on how to derive the final bounding boxes and their corresponding confidence scores, refer to this [post](http://machinethink.net/blog/object-detection-with-yolo/).

##  Solution
The GUI application project `ObjectDetection` can be used to to identify objects in live images based on the **Tiny Yolo2 ONNX** model. 

Again, note that this sample only uses/consumes a pre-trained ONNX model with ML.NET API. Therefore, it does **not** train any ML.NET model. Currently, ML.NET supports only for scoring/detecting with existing ONNX trained models. 

##  Code Walkthrough
There is a single project in the solution named `ObjectDetection`, which is responsible for loading the model in Tiny Yolo2 ONNX format and then detects objects in the stream.

### ML.NET: Model Scoring

Define the schema of data in a class type and refer that type while loading data using TextLoader. Here the class type is **ImageNetData**. 

```csharp
public class ImageNetData
{
        // Dimensions provided here seems not to play an important role
        [ImageType(480, 640)]
        public Bitmap InputImage { get; set; }

        public string Label { get; set; }

        public ImageNetData() 
        {
            InputImage = null;
            Label = "";
        }
}
```

### ML.NET: Configure the model

Code for working with the model is found in `OnnxModelScorer.cs` file, `LoadModel` method.

The first step is to create an empty dataview as we just need schema of data while configuring up model.

```csharp
ImageNetData[] inMemoryCollection = new ImageNetData[]
{
    new ImageNetData
    {
        InputImage = null,
        Label = ""
    }
};
var data = mlContext.Data.LoadFromEnumerable<ImageNetData>(inMemoryCollection);
```

It is important to highlight that the `Label` in the `ImageNetData` class is not really used when scoring with the Tiny Yolo2 Onnx model.

The second step is to define the estimator pipeline. Usually, when dealing with deep neural networks, you must adapt the images to the format expected by the network. This is the reason images are resized and then transformed (mainly, pixel values are normalized across all R,G,B channels).

```csharp
var pipeline = mlContext.Transforms.ResizeImages(outputColumnName: "image", imageWidth: ImageNetSettings.imageWidth, imageHeight: ImageNetSettings.imageHeight, inputColumnName: "InputImage")
                .Append(mlContext.Transforms.ExtractPixels(outputColumnName: "image"))
                .Append(mlContext.Transforms.ApplyOnnxModel(modelFile: modelLocation, outputColumnNames: new[] { TinyYoloModelSettings.ModelOutput }, inputColumnNames: new[] { TinyYoloModelSettings.ModelInput }));
```

You also need to check the neural network, and check the names of the input / output nodes. In order to inspect the model, you can use tools like [Netron](https://github.com/lutzroeder/netron), which is automatically installed with [Visual Studio Tools for AI](https://visualstudio.microsoft.com/downloads/ai-tools-vs/). 
These names are used later in the definition of the estimation pipe: in the case of the inception network, the input tensor is named 'image' and the output is named 'grid'

Define the **input** and **output** parameters of the Tiny Yolo2 Onnx Model.

```csharp
public struct TinyYoloModelSettings
{
    // for checking TIny yolo2 Model input and  output  parameter names,
    //you can use tools like Netron, 
    // which is installed by Visual Studio AI Tools

    // input tensor name
    public const string ModelInput = "image";

    // output tensor name
    public const string ModelOutput = "grid";
}
```

![inspecting neural network with netron](./docs/Netron/netron.PNG)

Finally, we return the trained model after *fitting* the estimator pipeline. 

```csharp
  var model = pipeline.Fit(data);
  return model;
```
When obtaining the prediction, we get an array of floats in the property `PredictedLabels`. The array is a float array of size **21125**. This is the output of model i,e 125x13x13 as discussed earlier. This output is interpreted by `YoloOutputParser` class and returns a number of bounding boxes for each image. Again these boxes are filtered so that we retrieve only 5 bounding boxes which have better confidence(how much certain that a box contains the obejct) for each object of the image. On console we display the label value of each bounding box.

# Detect objects in the image:

After the model is configured, we need to pass the image to the model to detect objects. When obtaining the prediction, we get an array of floats in the property `PredictedLabels`. The array is a float array of size **21125**. This is the output of model i,e 125x13x13 as discussed earlier. This output is interpreted by `YoloOutputParser` class and returns a number of bounding boxes for each image. Again these boxes are filtered so that we retrieve only 5 bounding boxes which have better confidence(how much certain that a box contains the obejct) for each object of the image. 

```csharp
IEnumerable<float[]> probabilities = modelScorer.Score(imageDataView);

YoloOutputParser parser = new YoloOutputParser();

var boundingBoxes =
    probabilities
    .Select(probability => parser.ParseOutputs(probability))
    .Select(boxes => parser.FilterBoundingBoxes(boxes, 5, .5F));
```

**Note** The Tiny Yolo2 model is not having much accuracy compare to full YOLO2 model. As this is a sample program we are using Tiny version of Yolo model i.e Tiny_Yolo2


