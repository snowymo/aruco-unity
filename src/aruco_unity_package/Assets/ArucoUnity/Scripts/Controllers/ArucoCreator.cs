﻿using ArucoUnity.Plugin;
using UnityEngine;
using System.IO;
using ArucoUnity.Objects;

namespace ArucoUnity
{
  /// \addtogroup aruco_unity_package
  /// \{

  namespace Controllers
  {
    /// <summary>
    /// Create and display images of an ArUco object ready to be printed.
    /// 
    /// See the OpenCV documentation for more information about the marker creation (second section of the following tutorial):
    /// http://docs.opencv.org/3.2.0/d5/dae/tutorial_aruco_detection.html
    /// </summary>
    [ExecuteInEditMode]
    public class ArucoCreator : MonoBehaviour
    {
      // Editor fields

      [SerializeField]
      [Tooltip("The ArUco object to create.")]
      protected ArucoObject arucoObject;

      [SerializeField]
      [Tooltip("Create the image and the image texture automatically at start.")]
      private bool createAtStart;

      [SerializeField]
      [Tooltip("Display the created image.")]
      private bool drawImage;

      [SerializeField]
      [Tooltip("Save the created image.")]
      private bool saveImage;

      [SerializeField]
      [Tooltip("The output folder for the saved image, relative to the Application.persistentDataPath folder.")]
      private string outputFolder = "ArucoUnity/Images/";

      [SerializeField]
      [Tooltip("The saved image name. The extension (.png) is added automatically. If empty, it will be generated automatically from the ArUco object.")]
      private string optionalImageFilename;

      // Properties

      /// <summary>
      /// The ArUco object to create.
      /// </summary>
      protected ArucoObject ArucoObject { get { return arucoObject; } set { SetArucoObject(value); } }

      /// <summary>
      /// Create the image and the image texture automatically at start.
      /// </summary>
      public bool CreateAtStart { get { return createAtStart; } set { createAtStart = value; } }

      /// <summary>
      /// Display the created image.
      /// </summary>
      public bool DrawImage { get { return drawImage; } set { drawImage = value; } }

      /// <summary>
      /// Save the created image.
      /// </summary>
      public bool SaveImage { get { return saveImage; } set { saveImage = value; } }

      /// <summary>
      /// The output folder for the saved image, relative to the Application.persistentDataPath folder (default: ArucoUnity/Images/).
      /// </summary>
      public string OutputFolder { get { return outputFolder; } set { outputFolder = value; } }

      /// <summary>
      /// The saved image name. The extension (.png) is added automatically. If null, it will be generated automatically.
      /// </summary>
      public string ImageFilename { get { return optionalImageFilename; } set { optionalImageFilename = value; } }

      /// <summary>
      /// The created image of the <see cref="ArucoObject"/>.
      /// </summary>
      public Cv.Mat Image { get; protected set; }

      /// <summary>
      /// The created texture of the <see cref="ArucoObject"/>.
      /// </summary>
      public Texture2D ImageTexture { get; protected set; }

      // Variables

      protected static GameObject arucoCreatorImagePlane;
      protected GameObject imagePlane;
      protected string imagePlaneName = "ImagePlane";
      protected Renderer imagePlaneRenderer;

      // MonoBehaviour methods

      protected virtual void Awake()
      {
        if (arucoCreatorImagePlane == null)
        {
          arucoCreatorImagePlane = Resources.Load("ArucoCreatorImagePlane") as GameObject;
        }

        if (imagePlane == null)
        {
          var imagePlaneTransform = transform.Find(imagePlaneName);
          if (imagePlaneTransform != null)
          {
            imagePlane = imagePlaneTransform.gameObject;
          }
          else
          {
            imagePlane = Instantiate(arucoCreatorImagePlane, transform);
            imagePlane.name = "ImagePlane";
            imagePlane.transform.localPosition = Vector3.zero;
            imagePlane.transform.localRotation = Quaternion.identity;
            imagePlane.transform.localScale = Vector3.one;
          }
          imagePlaneRenderer = imagePlane.GetComponent<Renderer>();

          imagePlane.SetActive(false);
        }
      }

      /// <summary>
      /// Create, draw and save the image of the <see cref="ArucoObject"/> at start if specified by the user.
      /// </summary>
      protected virtual void Start()
      {
        if (ArucoObject)
        {

#if UNITY_EDITOR
          if (Application.isPlaying)
          {
            if (CreateAtStart)
            {
              ArucoObject.PropertyUpdated += ArucoObject_PropertyUpdated;
#endif
              Create();

              if (DrawImage)
              {
                Draw();
              }

              if (SaveImage)
              {
                Save();
              }
#if UNITY_EDITOR
            }
          }
#endif
        }
      }

      protected virtual void OnDestroy()
      {
        if (ArucoObject)
        {
          ArucoObject.PropertyUpdated -= ArucoObject_PropertyUpdated;
        }
      }

      // Methods

      /// <summary>
      /// Create the image and the image texture of the <see cref="ArucoObject"/>.
      /// </summary>
      public virtual void Create()
      {
        Cv.Mat image = null;
        ImageTexture = null;

        // In case of a marker
        ArucoMarker marker = ArucoObject as ArucoMarker;
        if (marker != null)
        {
          marker.Dictionary.DrawMarker(marker.MarkerId, (int)marker.MarkerSideLength, out image, marker.MarkerBorderBits);
        }

        // In case of a grid board
        ArucoGridBoard arucoGridBoard = ArucoObject as ArucoGridBoard;
        if (arucoGridBoard != null)
        {
          Aruco.GridBoard gridBoard = arucoGridBoard.Board as Aruco.GridBoard;
          gridBoard.Draw(arucoGridBoard.ImageSize, out image, arucoGridBoard.MarginsSize, arucoGridBoard.MarkerBorderBits);
        }

        // In case of a charuco board
        ArucoCharucoBoard arucoCharucoBoard = ArucoObject as ArucoCharucoBoard;
        if (arucoCharucoBoard != null)
        {
          Aruco.CharucoBoard charucoBoard = arucoCharucoBoard.Board as Aruco.CharucoBoard;
          charucoBoard.Draw(arucoCharucoBoard.ImageSize, out image, arucoCharucoBoard.MarginsSize, arucoCharucoBoard.MarkerBorderBits);
        }

        // In case of a diamond
        ArucoDiamond diamond = ArucoObject as ArucoDiamond;
        if (diamond != null && diamond.Ids.Length == 4)
        {
          Cv.Vec4i ids = new Cv.Vec4i();
          for (int i = 0; i < diamond.Ids.Length; ++i)
          {
            ids.Set(i, diamond.Ids[i]);
          }
          Aruco.DrawCharucoDiamond(diamond.Dictionary, ids, (int)diamond.SquareSideLength, (int)diamond.MarkerSideLength, out image);
        }

        // Set the properties
        Image = image;
        if (Image != null)
        {
          // Vertical flip to correctly display the image on the texture
          int verticalFlipCode = 0;
          Cv.Mat imageForTexture = Image.Clone();
          Cv.Flip(imageForTexture, imageForTexture, verticalFlipCode);

          // Load the image to the texture
          int markerDataSize = (int)(Image.ElemSize() * Image.Total());
          ImageTexture = new Texture2D(Image.Cols, Image.Rows, TextureFormat.RGB24, false);
          ImageTexture.LoadRawTextureData(imageForTexture.DataIntPtr, markerDataSize);
          ImageTexture.Apply();
        }
      }

      /// <summary>
      /// Draw the <see cref="ImageTexture"/> on the <see cref="ImagePlane"/>.
      /// </summary>
      public virtual void Draw()
      {
        imagePlane.SetActive(true);
        imagePlaneRenderer.material.mainTexture = ImageTexture;
      }

      /// <summary>
      /// Save the <see cref="ImageTexture"/> on a image file in the <see cref="OutputFolder"/> with <see cref="ImageFilename"/> as filename.
      /// </summary>
      public virtual void Save()
      {
        if (ImageFilename == null || ImageFilename.Length == 0)
        {
          ImageFilename = GenerateImageFilename();
        }

        string outputFolderPath = Path.Combine((Application.isEditor) ? Application.dataPath : Application.persistentDataPath, OutputFolder);
        if (!Directory.Exists(outputFolderPath))
        {
          Directory.CreateDirectory(outputFolderPath);
        }

        string imageFilePath = outputFolderPath + ImageFilename;
        File.WriteAllBytes(imageFilePath, ImageTexture.EncodeToPNG());
      }

      /// <summary>
      /// Returns a generated filemame with the <see cref="ArucoObject"/> properties.
      /// </summary>
      public virtual string GenerateImageFilename()
      {
        string imageFilename = "ArUcoUnity_";

        ArucoMarker marker = ArucoObject as ArucoMarker;
        if (marker != null)
        {
          imageFilename += "Marker_" + marker.Dictionary.Name + "_Id_" + marker.MarkerId;
        }

        ArucoGridBoard gridBoard = ArucoObject as ArucoGridBoard;
        if (gridBoard != null)
        {
          imageFilename += "GridBoard_" + gridBoard.Dictionary.Name + "_X_" + gridBoard.MarkersNumberX + "_Y_" + gridBoard.MarkersNumberY
            + "_MarkerSize_" + gridBoard.MarkerSideLength;
        }

        ArucoCharucoBoard charucoBoard = ArucoObject as ArucoCharucoBoard;
        if (charucoBoard != null)
        {
          imageFilename += "ChArUcoBoard_" + charucoBoard.Dictionary.Name + "_X_" + charucoBoard.SquaresNumberX
            + "_Y_" + charucoBoard.SquaresNumberY + "_SquareSize_" + charucoBoard.SquareSideLength
            + "_MarkerSize_" + charucoBoard.MarkerSideLength;
        }

        ArucoDiamond diamond = ArucoObject as ArucoDiamond;
        if (diamond != null && diamond.Ids.Length == 4)
        {
          imageFilename += "DiamondMarker_" + diamond.Dictionary.Name + "_Ids_" + diamond.Ids[0] + "_" + diamond.Ids[1] + "_" + diamond.Ids[2] + "_"
            + diamond.Ids[3] + "_SquareSize_" + diamond.SquareSideLength + "_MarkerSize_" + diamond.MarkerSideLength;
        }

        imageFilename += ".png";

        return imageFilename;
      }

      protected virtual void SetArucoObject(ArucoObject arucoObject)
      {
        if (ArucoObject != null)
        {
          ArucoObject.PropertyUpdated -= ArucoObject_PropertyUpdated;
        }

        this.arucoObject = arucoObject;
        if (ArucoObject != null)
        {
          ArucoObject.PropertyUpdated += ArucoObject_PropertyUpdated;
        }
      }

      protected virtual void ArucoObject_PropertyUpdated(ArucoObject arucoObject)
      {
          Create();

          if (DrawImage)
          {
            Draw();
          }

#if UNITY_EDITOR
          if (Application.isPlaying)
          {
#endif
            if (SaveImage)
            {
              Save();
            }

#if UNITY_EDITOR
          }
#endif
      }
    }
  }

  /// \} aruco_unity_package
}