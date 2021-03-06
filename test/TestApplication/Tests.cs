﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Tests.cs" company="MapWindow Open Source GIS">
//   MapWindow developers community - 2014
// </copyright>
// <summary>
//   Static class to hold the tests methods
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace TestApplication
{
    #region

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows.Forms;

    using AxMapWinGIS;

    using MapWinGIS;

    using NCalc;

    #endregion

    /// <summary>Static class to hold the tests methods</summary>
    internal static class Tests
    {
        #region Static Fields

        /// <summary>
        /// The random generator.
        /// </summary>
        private static readonly Random RandomGenerator = new Random();

        /// <summary>Are the tiles loaded or not</summary>
        private static bool tilesAreLoaded;

        #endregion

        #region Properties

        /// <summary>
        ///     Gets or sets Map.
        /// </summary>
        internal static AxMap MyAxMap { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Tests the Clear method of the AxMap class
        /// </summary>
        /// <param name="shapefileLocation">
        /// Location of a shapefile
        /// </param>
        /// <param name="theForm">
        /// The main form of the application
        /// </param>
        /// <returns>
        /// True on success
        /// </returns>
        internal static bool AxMapClear(string shapefileLocation, Form1 theForm)
        {
            try
            {
                // Check the inputs
                if (!Helper.CheckShapefileLocation(shapefileLocation, theForm))
                {
                    return false;
                }

                // Add the shapefile as a layer
                Fileformats.OpenShapefileAsLayer(shapefileLocation, theForm, true);
                Application.DoEvents();
                Thread.Sleep(1000);

                // Draw something on the map
                var drawHandle = MyAxMap.NewDrawing(tkDrawReferenceList.dlScreenReferencedList);
                MyAxMap.DrawCircleEx(drawHandle, 50, 50, 50, 0x0, true);
                Application.DoEvents();
                Thread.Sleep(1000);

                // Change some of the settings
                MyAxMap.ShowVersionNumber = true;
                MyAxMap.ShowCoordinates = tkCoordinatesDisplay.cdmDegrees;

                // Get the layer count
                var layerCount = MyAxMap.NumLayers;

                // Get the new projection after adding the shapefile
                var shapeProj = MyAxMap.Projection;

                // Clear everything from the map
                MyAxMap.Clear();
                Application.DoEvents();

                // Test the projection
                if (shapeProj == tkMapProjection.PROJECTION_NONE)
                {
                    theForm.Progress(string.Empty, 10, "Could not test Projection.");
                }
                else if (MyAxMap.Projection != tkMapProjection.PROJECTION_NONE)
                {
                    theForm.Error(string.Empty, "Failed to reset Projection to default.");
                    return false;
                }
                else
                {
                    theForm.Progress(string.Empty, 10, "Successfully reset Projection to default.");
                }

                // Test the settings
                if (MyAxMap.ShowVersionNumber || MyAxMap.ShowCoordinates != tkCoordinatesDisplay.cdmAuto)
                {
                    theForm.Error(string.Empty, "Failed to reset settings to defaults.");
                    return false;
                }

                theForm.Progress(string.Empty, 20, "Successfully reset settings to defaults.");

                // Make sure all the layers are removed
                if (layerCount != 2)
                {
                    theForm.Progress(string.Empty, 30, "Could not test removing layers.");
                }
                else if (MyAxMap.NumLayers != 0)
                {
                    theForm.Error(string.Empty, "Failed to remove all layers.");
                    return false;
                }
                else
                {
                    theForm.Progress(string.Empty, 30, "Successfully removed all layers.");
                }
            }
            catch (Exception ex)
            {
                theForm.Error(string.Empty, "Exception: " + ex.Message);
            }

            return true;
        }

        /// <summary>
        /// Rasterize the shapefile
        /// </summary>
        /// <param name="shapefilename">
        /// The textfileLocation.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True on success
        /// </returns>
        internal static bool RasterizeShapefile(string shapefilename, Form1 theForm)
        {
            try
            {
                // Check inputs:
                if (!Helper.CheckShapefileLocation(shapefilename, theForm))
                {
                    return false;
                }

                // First check if the MWShapeID field is present:
                var sf = Fileformats.OpenShapefile(shapefilename, theForm);
                if (sf == null)
                {
                    return false;
                }

                // Get target resolution. The values must be expressed in georeferenced units (-tr):
                double minX, minY, minZ, maxX, maxY, maxZ;
                sf.Extents.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);

                const string FieldName = "MWShapeID";
                if (sf.Table.FieldIndexByName[FieldName] == -1)
                {
                    theForm.Progress(string.Empty, 100, "Adding " + FieldName + " as field");

                    if (!sf.StartEditingShapes(true, theForm))
                    {
                        theForm.Error(
                            string.Empty, 
                            "Could not put shapefile in edit mode: " + sf.ErrorMsg[sf.LastErrorCode]);
                        return false;
                    }

                    if (sf.EditAddField(FieldName, FieldType.INTEGER_FIELD, 0, 10) == -1)
                    {
                        theForm.Error(string.Empty, "Could not add the fieldname: " + sf.ErrorMsg[sf.LastErrorCode]);
                        return false;
                    }

                    if (!sf.StopEditingShapes(true, true, theForm))
                    {
                        theForm.Error(
                            string.Empty,
                            "Could not end shapefile in edit mode: " + sf.ErrorMsg[sf.LastErrorCode]);
                        return false;
                    }
                }

                if (!sf.Close())
                {
                    theForm.Error(string.Empty, "Could not close the shapefile: " + sf.ErrorMsg[sf.LastErrorCode]);
                    return false;
                }

                var folder = Path.GetDirectoryName(shapefilename);
                var utils = new Utils { GlobalCallback = theForm };
                var globalSettings = new GlobalSettings();
                globalSettings.ResetGdalError();
                if (folder != null)
                {
                    var outputFile = Helper.CreateOutputFilename(shapefilename, "rasterized");
                    outputFile = Path.ChangeExtension(outputFile, ".tif");
                    if (File.Exists(outputFile))
                    {
                        File.Delete(outputFile);
                    }

                    var options = string.Format(
                        "-a {0} -l {1} -of GTiff -a_nodata -999 -init -999 -ts 800 800", 
                        FieldName, 
                        Path.GetFileNameWithoutExtension(shapefilename));
                    Debug.WriteLine(options);
                    if (!utils.GDALRasterize(shapefilename, outputFile, options, theForm))
                    {
                        var msg = " in GDALRasterize: " + utils.ErrorMsg[utils.LastErrorCode];
                        if (globalSettings.GdalLastErrorMsg != string.Empty)
                        {
                            msg += Environment.NewLine + "GdalLastErrorMsg: " + globalSettings.GdalLastErrorMsg;
                        }

                        theForm.Error(string.Empty, msg);
                        return false;
                    }

                    // Open the files:
                    Fileformats.OpenImageAsLayer(outputFile, theForm, true);
                }
            }
            catch (Exception exception)
            {
                theForm.Error(string.Empty, "Exception: " + exception.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Run Aggregate shapefile test
        /// </summary>
        /// <param name="textfileLocation">
        /// The location of the shapefile.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True when no errors
        /// </returns>
        internal static bool RunAggregateShapefileTest(string textfileLocation, Form1 theForm)
        {
            theForm.Progress(
                string.Empty,
                0,
                string.Format("-----------------------The Aggregate shapefile test has started.{0}", Environment.NewLine));

            var retVal = true;

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);

            // Get every first line and second line:
            for (var i = 0; i < lines.Count; i = i + 2)
            {
                if (i + 1 > lines.Count)
                {
                    theForm.Error(string.Empty, "Input file is incorrect. Not enough lines");
                    break;
                }

                int fieldIndex;
                if (!int.TryParse(lines[i + 1], out fieldIndex))
                {
                    theForm.Error(string.Empty, "Input file is incorrect. Can't find field index value");
                    break;
                }

                if (!AggregateShapefile(lines[i], fieldIndex, theForm))
                {
                    retVal = false;
                }

                Application.DoEvents();
                Thread.Sleep(1000);
            }

            theForm.Progress(string.Empty, 100, "The Aggregate shapefile test has finished.");

            return retVal;
        }

        /// <summary>
        /// Run the clear the map test
        /// </summary>
        /// <param name="textfileLocation">
        /// The location of the text file.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        internal static bool RunAxMapClearTest(string textfileLocation, Form1 theForm)
        {
            var numErrors = 0;

            // Open text file:
            if (!File.Exists(textfileLocation))
            {
                throw new FileNotFoundException("Cannot find text file.", textfileLocation);
            }

            theForm.Progress(string.Empty, 0, "-----------------------The Clear the map tests have started.");

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);
            foreach (var line in lines)
            {
                if (!AxMapClear(line, theForm))
                {
                    numErrors++;
                }

                // Wait a second to show something:
                Application.DoEvents();
                Thread.Sleep(1000);
            }

            theForm.Progress(
                string.Empty, 
                100, 
                string.Format("The Clear the map tests have finished, with {0} errors", numErrors));

            return numErrors == 0;
        }

        /// <summary>
        /// Run the Clip grid by polygon test
        /// </summary>
        /// <param name="textfileLocation">
        /// The textfileLocation location.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True when no errors
        /// </returns>
        internal static bool RunClipGridByPolygonTest(string textfileLocation, Form1 theForm)
        {
            theForm.Progress(
                string.Empty, 
                0, 
                string.Format("-----------------------The Clip grid by polygon test has started.{0}", Environment.NewLine));

            var retVal = true;

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);

            // Get every first line and second line:
            for (var i = 0; i < lines.Count; i = i + 2)
            {
                if (i + 1 > lines.Count)
                {
                    theForm.Error(string.Empty, "Input file is incorrect.");
                    retVal = false;
                    break;
                }

                if (!ClipGridByPolygon(lines[i], lines[i + 1], theForm))
                {
                    retVal = false;
                }
            }

            theForm.Progress(string.Empty, 100, "The Clip grid by polygon test has finished.");

            return retVal;
        }

        /// <summary>
        /// Run the grid proxy test
        /// </summary>
        /// <param name="textfileLocation">
        /// The textfileLocation location.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True on success
        /// </returns>
        internal static bool RunGridProxyTest(string textfileLocation, Form1 theForm)
        {
            var numErrors = 0;

            // Open text file:
            if (!File.Exists(textfileLocation))
            {
                throw new FileNotFoundException("Cannot find text file.", textfileLocation);
            }

            theForm.Progress(string.Empty, 0, "-----------------------The grid proxy tests have started.");

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);

            // Get every first and second line:
            for (var i = 0; i < lines.Count; i = i + 2)
            {
                if (i + 1 > lines.Count)
                {
                    theForm.Error(string.Empty, "Input file is incorrect. Not enough lines");
                    break;
                }

                MyAxMap.Clear();
                Application.DoEvents();

                var grid = new Grid { GlobalCallback = theForm };
                var filename = lines[i];
                var colorScheme = Convert.ToInt32(lines[i + 1]);
                theForm.Progress(
                    string.Empty, 
                    100, 
                    string.Format("++++++++++++++++++ Loading grid with color scheme {0}", (PredefinedColorScheme)colorScheme));
                if (grid.Open(filename, GridDataType.UnknownDataType, true, GridFileType.UseExtension, theForm))
                {
                    // If it already has a proxy file, remove it first:
                    if (grid.HasValidImageProxy)
                    {
                        grid.RemoveImageProxy();
                    }

                    var scheme = grid.GenerateColorScheme(
                        tkGridSchemeGeneration.gsgGradient, 
                        (PredefinedColorScheme)colorScheme);
                    theForm.Progress(string.Empty, 100, "Scheme: " + scheme.Serialize());
                    theForm.Progress(string.Empty, 100, "Open grid as image");
                    var img = grid.OpenAsImage(scheme, tkGridProxyMode.gpmAuto, theForm);
                    if (img != null)
                    {
                        img.UpsamplingMode = tkInterpolationMode.imHighQualityBilinear;
                        img.DownsamplingMode = tkInterpolationMode.imHighQualityBilinear;
                        theForm.Progress(string.Empty, 100, "Adding to map");
                        var handle = MyAxMap.AddLayer(img, true);
                        if (handle == -1)
                        {
                            numErrors++;
                        }

                        // Wait to show the map:
                        Application.DoEvents();
                    }

                    grid.Close(); // we no longer need it as Image class is used for rendering
                }

                // Wait a second to show something:
                Application.DoEvents();
                theForm.Progress(string.Empty, 100, "Finished adding to map");
                Thread.Sleep(1000);
            }

            theForm.Progress(
                string.Empty, 
                100, 
                string.Format("The grid proxy tests have finished. {0} tests failed", numErrors));

            return numErrors == 0;
        }

        /// <summary>
        /// Run grid open tests
        /// </summary>
        /// <param name="textfileLocation">
        /// The textfileLocation location.
        /// </param>
        /// <param name="theForm">
        /// The form with the callback implementation.
        /// </param>
        /// <exception cref="FileNotFoundException">
        /// When the file is not found
        /// </exception>
        /// <returns>
        /// True when no errors
        /// </returns>
        internal static bool RunGridfileTest(string textfileLocation, Form1 theForm)
        {
            var retVal = true;

            // Open text file:
            if (!File.Exists(textfileLocation))
            {
                throw new FileNotFoundException("Cannot find text file.", textfileLocation);
            }

            theForm.Progress(string.Empty, 0, "-----------------------The grid open tests have started.");

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);
            foreach (var line in lines)
            {
                // Open image:
                if (Fileformats.OpenGridAsLayer(line, theForm, true) == -1)
                {
                    retVal = false;
                }

                // Wait a second to show something:
                Application.DoEvents();
                Thread.Sleep(1000);
            }

            theForm.Progress(string.Empty, 100, "The grid open tests have finished.");

            return retVal;
        }

        /// <summary>
        /// Run image open tests
        /// </summary>
        /// <param name="textfileLocation">
        /// The textfileLocation location.
        /// </param>
        /// <param name="theForm">
        /// The form with the callback implementation.
        /// </param>
        /// <exception cref="FileNotFoundException">
        /// When the file is not found
        /// </exception>
        /// <returns>
        /// True when no errors.
        /// </returns>
        internal static bool RunImagefileTest(string textfileLocation, Form1 theForm)
        {
            var retVal = true;

            // Open text file:
            if (!File.Exists(textfileLocation))
            {
                throw new FileNotFoundException("Cannot find text file.", textfileLocation);
            }

            theForm.Progress(string.Empty, 0, "-----------------------The image open tests have started.");

            // Read text file:
            var count = 1;
            var lines = Helper.ReadTextfile(textfileLocation);
            foreach (var line in lines)
            {
                theForm.Progress(string.Empty, 100, " ");
                theForm.Progress(string.Empty, 100, "Image: " + count);
                theForm.Progress(string.Empty, 100, "-------------------------------");
                count++;

                // Open image:
                var hndle = Fileformats.OpenImageAsLayer(line, theForm, true);

                if (hndle == -1)
                {
                    retVal = false;
                    continue;
                }

                // Wait a second to show something:
                Application.DoEvents();
                Thread.Sleep(1000);

                // now do some zooming:
                var img = MyAxMap.get_Image(hndle);
                DoSomeZooming(theForm, img.NumOverviews);
            }

            theForm.Progress(string.Empty, 100, "The image open tests have finished.");

            return retVal;
        }

        /// <summary>
        /// The run grid to image test.
        /// </summary>
        /// <param name="textfileLocation">
        /// The textfile location.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True on success
        /// </returns>
        internal static bool RunGridToImageTest(string textfileLocation, Form1 theForm)
        {
            var numErrors = 0;

            // Open text file:
            if (!File.Exists(textfileLocation))
            {
                throw new FileNotFoundException("Cannot find text file.", textfileLocation);
            }

            theForm.Progress(string.Empty, 0, "-----------------------The GridToImage open tests have started.");

            var gs = new GlobalSettings { GridProxyFormat = tkGridProxyFormat.gpfTiffProxy };
            
            gs.ImageDownsamplingMode = tkInterpolationMode.imBicubic;
            gs.ImageUpsamplingMode = tkInterpolationMode.imBicubic;
            gs.RasterOverviewCreation = tkRasterOverviewCreation.rocYes;
            gs.RasterOverviewResampling = tkGDALResamplingMethod.grmBicubic;
            gs.TiffCompression = tkTiffCompression.tkmLZW;

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);
            foreach (var line in lines)
            {
                var grid = Fileformats.OpenGrid(line, theForm);
                if (grid == null)
                {
                    numErrors++;
                    continue;
                }

                var gcs = grid.GenerateColorScheme(tkGridSchemeGeneration.gsgGradient, PredefinedColorScheme.SummerMountains);
                if (gcs == null)
                {
                    grid.Close();
                    theForm.Error("Failed to generated color scheme.");
                    numErrors++;
                    continue;
                }

                // Create image from grid with color scheme:
                var img = grid.OpenAsImage(gcs, tkGridProxyMode.gpmUseProxy);
                if (img == null)
                {
                    grid.Close();
                    theForm.Error("Failed to create proxy." + grid.ErrorMsg[grid.LastErrorCode]);
                    numErrors++;
                    continue;
                }

                var newFilename = Helper.CreateOutputFilename(line, "proxy");
                newFilename = Path.ChangeExtension(newFilename, ".tif");
                if (File.Exists(newFilename))
                {
                    File.Delete(newFilename);
                }
                
                if (!img.Save(newFilename, true))
                {
                    theForm.Error("Failed to save proxy: " + img.ErrorMsg[img.LastErrorCode]);
                    img.Close();
                }
                else
                {
                    theForm.Progress("Saved proxy as " + newFilename);

                    // Get info of new file:
                    var utils = new UtilsClass();
                    var gdalInfo = utils.GDALInfo(newFilename, "-stats", theForm);
                    theForm.Progress("GDALInfo: " + gdalInfo);

                    // Add proxy to map:
                    MyAxMap.AddLayer(img, true);

                    // Wait a second to show something:
                    Application.DoEvents();
                    Thread.Sleep(1000);
                }

                // Don't close image because it is opened as layer:
                // img.Close();
                grid.Close();
            }

            theForm.Progress(string.Empty, 100, string.Format("The GridToImage tests have finished with {0} errors", numErrors));

            return numErrors == 0;
        }

        /// <summary>
        /// The run sf save as test.
        /// </summary>
        /// <param name="textfileLocation">
        /// The textfile location.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        internal static bool RunSfSaveAsTest(string textfileLocation, Form1 theForm)
        {
            var numErrors = 0;

            // Open text file:
            if (!File.Exists(textfileLocation))
            {
                throw new FileNotFoundException("Cannot find text file.", textfileLocation);
            }

            theForm.Progress(string.Empty, 0, "-----------------------The Shapefile SaveAs tests have started.");

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);
            foreach (var line in lines)
            {
                var sf = Fileformats.OpenShapefile(line, theForm);
                if (sf == null)
                {
                    numErrors++;
                    continue;
                }

                var newFilename = Helper.CreateOutputFilename(line, "Домены");
                Helper.DeleteShapefile(newFilename);
                var newFilenameWithoutExtension = Path.GetFileNameWithoutExtension(newFilename);
                if (File.Exists(newFilename))
                {
                    File.Delete(newFilename);
                }

                if (sf.SaveAs(newFilenameWithoutExtension, theForm))
                {
                    theForm.Error("Save as should not work, because no extention");
                    numErrors++;
                    continue;
                }
                
                // sf.SaveAs should report an error in this case, but don't mark it as such
                // because else this test fails incorrectly:
                if (numErrors == 0)
                {
                    // No errors yet so reset:
                    theForm.TestHasErrors = false;
                }

                if (!sf.SaveAs(newFilename, theForm))
                {
                    theForm.Error("Failed to save as: " + sf.ErrorMsg[sf.LastErrorCode]);
                    numErrors++;
                    continue;
                }
                
                theForm.Progress("File name: " + sf.Filename);

                // Add sf to map:
                MyAxMap.AddLayer(sf, true);

                // Wait a second to show something:
                Application.DoEvents();
                Thread.Sleep(1000);
            }

            theForm.Progress(string.Empty, 100, string.Format("The Shapefile SaveAs tests have finished with {0} errors", numErrors));

            return numErrors == 0;
        }

        /// <summary>
        /// The run drawing layers test.
        /// </summary>
        /// <param name="textfileLocation">
        /// The textfile location.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True on success
        /// </returns>
        internal static bool RunDrawingLayersTest(string textfileLocation, Form1 theForm)
        {
            var numErrors = 0;

            // Open text file:
            if (!File.Exists(textfileLocation))
            {
                throw new FileNotFoundException("Cannot find text file.", textfileLocation);
            }

            theForm.Progress(string.Empty, 0, "-----------------------The Drawing layers tests have started.");

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);
            
            // Read all shapefiles at once:
            var filenames = lines.Where(File.Exists).ToList();

            if (filenames.Count > 0)
            {
                // Add last shapefile as normal layer:
                Fileformats.OpenShapefileAsLayer(filenames[filenames.Count - 1], theForm, true);

                // Wait a second to show something:
                Application.DoEvents();
                Thread.Sleep(1000);

                // Convert a few shapefiles to drawing layers:
                if (!ShapefilesToDrawingLayer(filenames, theForm))
                {
                    numErrors++;
                }

                // Add some random drawing items:
                if (!AddDrawingItems(theForm))
                {
                    numErrors++;
                }

                // Wait a second to show something:
                Application.DoEvents();
                Thread.Sleep(1000);
            }

            theForm.Progress(string.Empty, 100, string.Format("The Drawing layers tests have finished with {0} errors", numErrors));

            return numErrors == 0;
        }

        /// <summary>
        /// Run the OGRInfo test
        /// </summary>
        /// <param name="textfileLocation">
        /// The file.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True on success
        /// </returns>
        internal static bool RunOgrInfoTest(string textfileLocation, Form1 theForm)
        {
            var numErrors = 0;

            // Open text file:
            if (!File.Exists(textfileLocation))
            {
                throw new FileNotFoundException("Cannot find text file.", textfileLocation);
            }

            theForm.Progress(string.Empty, 0, "-----------------------The OGR Info tests have started.");

            var utils = new Utils { GlobalCallback = theForm };

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);
            foreach (var line in lines)
            {
                var output = utils.OGRInfo(line, "-so", Path.GetFileNameWithoutExtension(line), theForm);
                theForm.Progress(string.Empty, 100, output.Replace("\n", Environment.NewLine));
                if (string.IsNullOrEmpty(output))
                {
                    numErrors++;
                }

                // Wait a second to show something:
                Application.DoEvents();
                Thread.Sleep(1000);
            }

            theForm.Progress(
                string.Empty, 
                100, 
                string.Format("The OGR Info tests have finished, with {0} errors", numErrors));

            return numErrors == 0;
        }

        /// <summary>
        /// Run the raster calculator test
        /// </summary>
        /// <param name="textfileLocation">
        /// The textfileLocation location.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True on success 
        /// </returns>
        internal static bool RunRasterCalculatorTest(string textfileLocation, Form1 theForm)
        {
            theForm.Progress(string.Empty, 0, "-----------------------The Raster calculator test has started.");

            var retVal = true;

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);

            // Get every first, second and third line:
            for (var i = 0; i < lines.Count; i = i + 3)
            {
                if (i + 2 > lines.Count)
                {
                    theForm.Error(string.Empty, "Input file is incorrect. Not enough lines");
                    break;
                }

                retVal = RasterCalculatorTest(lines[i], lines[i + 1], lines[i + 2], theForm);

                Application.DoEvents();
                Thread.Sleep(1000);
            }

            theForm.Progress(string.Empty, 100, "The Raster calculator test has finished.");

            return retVal;
        }

        /// <summary>
        /// Run the Rasterize shapefile test
        /// </summary>
        /// <param name="textfileLocation">
        /// The textfileLocation location.
        /// </param>
        /// <param name="theForm">
        /// The form with the callback implementation.
        /// </param>
        /// <exception cref="FileNotFoundException">
        /// When the file is not found
        /// </exception>
        /// <returns>
        /// True when no errors.
        /// </returns>
        internal static bool RunRasterizeTest(string textfileLocation, Form1 theForm)
        {
            var numErrors = 0;

            // Open text file:
            if (!File.Exists(textfileLocation))
            {
                throw new FileNotFoundException("Cannot find text file.", textfileLocation);
            }

            theForm.Progress(string.Empty, 0, "-----------------------The rasterize shapfile tests have started.");

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);
            foreach (var line in lines)
            {
                if (!RasterizeShapefile(line, theForm))
                {
                    numErrors++;
                }

                // Wait a second to show something:
                Application.DoEvents();
                Thread.Sleep(2000);
            }

            theForm.Progress(
                string.Empty, 
                100, 
                string.Format("The  rasterize shapfile tests have finished. {0} tests failed", numErrors));

            return numErrors == 0;
        }

        /// <summary>
        /// Run the grid proxy test
        /// </summary>
        /// <param name="textfileLocation">
        /// The textfileLocation location.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True on success
        /// </returns>
        internal static bool RunReclassifyTest(string textfileLocation, Form1 theForm)
        {
            var numErrors = 0;

            // Open text file:
            if (!File.Exists(textfileLocation))
            {
                throw new FileNotFoundException("Cannot find text file.", textfileLocation);
            }

            theForm.Progress(string.Empty, 100, "-----------------------The reclassify tests have started.");

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);

            // Get every first and second line:
            for (var i = 0; i < lines.Count; i = i + 2)
            {
                if (i + 1 > lines.Count)
                {
                    theForm.Error(string.Empty, "Input file is incorrect. Not enough lines");
                    break;
                }

                MyAxMap.Clear();
                Application.DoEvents();

                var grid = new Grid { GlobalCallback = theForm };
                var filename = lines[i];
                var numClasses = Convert.ToInt32(lines[i + 1]);
                var output = Helper.CreateOutputFilename(filename, "reclassified");

                // Delete output file:
                Helper.DeleteGridfile(output);

                // Open grid to create reclassify array:);
                if (!grid.Open(filename, GridDataType.UnknownDataType, true, GridFileType.UseExtension, theForm))
                {
                    theForm.Error(string.Empty, "Coumd not load inout grid file. Ending test");
                    numErrors++;
                    continue;
                }

                // TODO: Read statistics from filename.aux.xml file, created after calling gdalinfo -stats
                var min = (double)grid.Minimum;
                var max = (double)grid.Maximum;

                // close the grid
                grid.Close();

                var difference = max - min;
                var step = difference / numClasses;

                var ut = new Utils();

                // TODO: Use the number of classes in the text file:
                var arr = new[]
                              {
                                  new { Low = min, High = min + step, NewValue = 40.0 }, 
                                  new { Low = min + step, High = min + (2 * step), NewValue = 60.0 }, 
                                  
                                  // new { Low = 40.0, High = 60.0, NewValue = 80.0 }, 
                                  // new { Low = 60.0, High = 80.0, NewValue = 100.0 },
                                  new { Low = min + (2 * step), High = max, NewValue = 120.0 }
                              };

                if (
                    !ut.ReclassifyRaster(
                        filename, 
                        1, 
                        output, 
                        arr.Select(j => j.Low).ToArray(), 
                        arr.Select(j => j.High).ToArray(), 
                        arr.Select(j => j.NewValue).ToArray(), 
                        "GTiff", 
                        theForm))
                {
                    theForm.Error(string.Empty, "Failed to reclassify: " + ut.ErrorMsg[ut.LastErrorCode]);
                }
                else
                {
                    theForm.Progress(string.Empty, 100, "Reclassified successfully");

                    // Open the result:
                    if (Fileformats.OpenGridAsLayer(output, theForm, true) == -1)
                    {
                        theForm.Error(string.Empty, "Cannot open the resulting grid");
                        numErrors++;
                    }

                    MyAxMap.Refresh();
                }

                // Wait a second to show something:
                Application.DoEvents();
                theForm.Progress(string.Empty, 100, "Finished adding to map");
                Thread.Sleep(1000);
            }

            theForm.Progress(
                string.Empty, 
                100, 
                string.Format("The reclassify tests have finished. {0} tests failed", numErrors));

            // MyAxMap.Redraw();
            return numErrors == 0;
        }

        /// <summary>
        /// Run shapefile open tests
        /// </summary>
        /// <param name="textfileLocation">
        /// The textfileLocation location.
        /// </param>
        /// <param name="theForm">
        /// The form with the callback implementation.
        /// </param>
        /// <exception cref="FileNotFoundException">
        /// When the file is not found
        /// </exception>
        /// <returns>
        /// True when no errors
        /// </returns>
        internal static bool RunShapefileTest(string textfileLocation, Form1 theForm)
        {
            var retVal = true;

            theForm.Progress(
                string.Empty, 
                0, 
                string.Format("-----------------------The shapefile open tests have started."));

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);

            foreach (var line in lines)
            {
                // Open shapefile:
                if (Fileformats.OpenShapefileAsLayer(line, theForm, true) == -1)
                {
                    // Something went wrong:
                    retVal = false;
                }

                // Sometimes causes a SEH exception: 
                try
                {
                    Application.DoEvents();
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    theForm.Error("Error in RunShapefileTest: " + ex.Message);
                }
            }

            theForm.Progress(string.Empty, 100, "The shapefile open tests have finished.");
            return retVal;
        }

        /// <summary>
        /// Run the Shapefile to grid test
        /// </summary>
        /// <param name="textfileLocation">
        /// The textfileLocation.
        /// </param>
        /// <param name="theForm">
        /// The the form.
        /// </param>
        /// <returns>
        /// True when no errors
        /// </returns>
        internal static bool RunShapefileToGridTest(string textfileLocation, Form1 theForm)
        {
            theForm.Progress(
                string.Empty, 
                0, 
                string.Format("-----------------------The Shapefile to grid test has started."));

            var numErrors = 0;

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);

            foreach (var line in lines)
            {
                MyAxMap.RemoveAllLayers();
                try
                {
                    // Check inputs:
                    if (!Helper.CheckShapefileLocation(line, theForm))
                    {
                        numErrors++;
                        continue;
                    }

                    var folder = Path.GetDirectoryName(line);
                    if (folder == null)
                    {
                        numErrors++;
                        continue;
                    }

                    var resultGridFilename = Path.Combine(folder, "ShapefileToGridTest.asc");
                    Helper.DeleteGridfile(resultGridFilename);

                    // Setup grid header:
                    const int NumCols = 100;
                    const int NumRows = 100;
                    var sf = Fileformats.OpenShapefile(line, theForm);
                    if (sf == null)
                    {
                        theForm.Error("Could not open shapefile: " + line);
                        numErrors++;
                        continue;
                    }

                    double minX, minY, minZ, maxX, maxY, maxZ;
                    sf.Extents.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
                    var gridHeader = new GridHeader
                                         {
                                             NodataValue = -1,
                                             NumberCols = NumCols,
                                             NumberRows = NumRows,
                                             Projection = sf.Projection,
                                             Notes = "Created using ShapefileToGrid",
                                             XllCenter = minX,
                                             YllCenter = minY,
                                             dX = (maxX - minX) / NumCols,
                                             dY = (maxY - minY) / NumRows
                                         };

                    var utils = new Utils { GlobalCallback = theForm };
                    var resultGrid = utils.ShapefileToGrid(sf, false, gridHeader);
                    if (resultGrid == null)
                    {
                        theForm.Error(
                            string.Empty,
                            "Error in ShapefileToGrid: " + utils.ErrorMsg[utils.LastErrorCode]);
                        numErrors++;
                        continue;
                    }

                    if (!resultGrid.Save(resultGridFilename, GridFileType.UseExtension, null))
                    {
                        theForm.Error(
                            string.Empty,
                            "Error in Grid.Save(): " + resultGrid.ErrorMsg[resultGrid.LastErrorCode]);
                        numErrors++;
                        continue;
                    }

                    Fileformats.OpenGridAsLayer(resultGridFilename, theForm, true);

                    // Open shapefile:
                    sf.DefaultDrawingOptions.LineColor = utils.ColorByName(tkMapColor.Black);
                    sf.DefaultDrawingOptions.LineWidth = 1;
                    sf.DefaultDrawingOptions.FillVisible = false;
                    sf.DefaultDrawingOptions.FillVisible = false;
                    sf.Labels.Visible = false;
                    
                    // TODO: How to NOT show the symbology:
                    MyAxMap.AddLayer(sf, true);

                    // Wait to show the map:
                    Application.DoEvents();
                    Thread.Sleep(1000);
                }
                catch (Exception exception)
                {
                    theForm.Error(string.Empty, "Exception: " + exception.Message);
                    numErrors++;
                }
            }

            theForm.Progress(string.Empty, 100, "The Shapefile to grid test has finished.");

            return numErrors == 0;
        }

        /// <summary>
        /// Run the Spatial Index tests
        /// </summary>
        /// <param name="textfileLocation">
        /// The textfileLocation location
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True on success
        /// </returns>
        internal static bool RunSpatialIndexTest(string textfileLocation, Form1 theForm)
        {
            var numErrors = 0;

            theForm.Progress(string.Empty, 0, "-----------------------The Spatial Index tests have started.");

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);

            foreach (var line in lines)
            {
                if (!SpatialIndexTest(line, theForm))
                {
                    numErrors++;
                }

                Application.DoEvents();
                Thread.Sleep(1000);
            }

            theForm.Progress(
                string.Empty, 
                100, 
                string.Format("The spatial index tests have finished. {0} tests failed.", numErrors));

            return numErrors == 0;
        }

        /// <summary>
        /// Run tiles load tests
        /// </summary>
        /// <param name="textfileLocation">
        /// The textfileLocation location.
        /// </param>
        /// <param name="theForm">
        /// The form with the callback implementation.
        /// </param>
        /// <returns>
        /// True when no errors.
        /// </returns>
        internal static bool RunTilesLoadTest(string textfileLocation, Form1 theForm)
        {
            theForm.Progress(string.Empty, 0, "-----------------------The tiles load tests have started.");

            var retVal = true;

            // Set up event handler:
            MyAxMap.TilesLoaded += MyAxMapTilesLoaded;

            // Enable logging:
            var logPath = Path.GetDirectoryName(Application.ExecutablePath) + "\\tiles.log";
            MyAxMap.Tiles.StartLogRequests(logPath);
            Debug.Print("Log is opened: " + MyAxMap.Tiles.LogIsOpened);

            // Enable tiling:
            MyAxMap.Tiles.GlobalCallback = theForm;
            MyAxMap.Tiles.Visible = true;
            MyAxMap.Tiles.AutodetectProxy();
            MyAxMap.Tiles.DoCaching[tkCacheType.Disk] = true;

            // probably better to turn if off otherwise on second run nothing will be downloaded (everything in cache)  
            MyAxMap.Tiles.UseCache[tkCacheType.Disk] = false;
            MyAxMap.Tiles.UseServer = true;

            // Do some logging
            theForm.Progress(string.Empty, 100, "DiskCacheFilename: " + MyAxMap.Tiles.DiskCacheFilename);
            theForm.Progress(
                string.Empty, 
                100, 
                "CheckConnection: " + MyAxMap.Tiles.CheckConnection("http://www.google.com"));

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);

            // Get every first line and second line:
            for (var i = 0; i < lines.Count; i = i + 2)
            {
                if (i + 1 > lines.Count)
                {
                    theForm.Error(string.Empty, "Input file is incorrect.");
                    retVal = false;
                    break;
                }

                int provId;

                if (!int.TryParse(lines[i + 1], out provId))
                {
                    theForm.Progress(string.Empty, 100, "Use custom provider");

                    // Add custom provider:
                    MyAxMap.Tiles.Providers.Clear(true);
                    provId = 1024 + MyAxMap.Tiles.Providers.Count;

                    // TODO: Make tkTileProjection flexible, grab it from the map projection:
                    if (!MyAxMap.Tiles.Providers.Add(provId, "Custom", lines[i + 1], tkTileProjection.Amersfoort, 0, 16))
                    {
                        theForm.Error(
                            string.Empty, 
                            "Provider add error: " + MyAxMap.Tiles.ErrorMsg[MyAxMap.Tiles.LastErrorCode]);
                        retVal = false;
                    }
                }

                // Load the tiles:
                if (!LoadTiles(lines[i], provId, theForm))
                {
                    retVal = false;
                }
            }

            if (retVal)
            {
                theForm.Progress("Tiles were loaded for all zoom levels");
            }
            else
            {
                theForm.Error(string.Empty, "Tiles weren't loaded for some zooms");
            }

            theForm.Progress(string.Empty, 100, "The tiles load tests have finished.");

            // Stop logging:
            MyAxMap.Tiles.StopLogRequests();

            return retVal;
        }

        /// <summary>
        /// Select a shapefile
        /// </summary>
        /// <param name="textBox">
        /// The text box.
        /// </param>
        /// <param name="title">
        /// The title.
        /// </param>
        internal static void SelectAnyfile(TextBox textBox, string title)
        {
            using (
                var ofd = new OpenFileDialog
                              {
                                  CheckFileExists = true, 
                                  Filter = @"All Files|*.*", 
                                  Multiselect = false, 
                                  SupportMultiDottedExtensions = true, 
                                  Title = title
                              })
            {
                if (textBox.Text != string.Empty)
                {
                    var folder = Path.GetDirectoryName(textBox.Text);
                    if (folder != null)
                    {
                        if (Directory.Exists(folder))
                        {
                            ofd.InitialDirectory = folder;
                        }
                    }
                }

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    textBox.Text = ofd.FileName;
                }
            }
        }

        /// <summary>
        /// Select a grid file
        /// </summary>
        /// <param name="textBox">
        /// The text box.
        /// </param>
        /// <param name="title">
        /// The title.
        /// </param>
        internal static void SelectGridfile(TextBox textBox, string title)
        {
            var grd = new Grid();

            using (
                var ofd = new OpenFileDialog
                              {
                                  CheckFileExists = true, 
                                  Filter = grd.CdlgFilter, 
                                  Multiselect = false, 
                                  SupportMultiDottedExtensions = true, 
                                  Title = title
                              })
            {
                if (textBox.Text != string.Empty)
                {
                    var folder = Path.GetDirectoryName(textBox.Text);
                    if (folder != null)
                    {
                        if (Directory.Exists(folder))
                        {
                            ofd.InitialDirectory = folder;
                        }
                    }
                }

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    textBox.Text = ofd.FileName;
                }
            }
        }

        /// <summary>
        /// Select a shapefile
        /// </summary>
        /// <param name="textBox">
        /// The text box.
        /// </param>
        /// <param name="title">
        /// The title.
        /// </param>
        internal static void SelectShapefile(TextBox textBox, string title)
        {
            using (
                var ofd = new OpenFileDialog
                              {
                                  CheckFileExists = true, 
                                  Filter = @"Shapefiles|*.shp", 
                                  Multiselect = false, 
                                  SupportMultiDottedExtensions = true, 
                                  Title = title
                              })
            {
                if (textBox.Text != string.Empty)
                {
                    var folder = Path.GetDirectoryName(textBox.Text);
                    if (folder != null)
                    {
                        if (Directory.Exists(folder))
                        {
                            ofd.InitialDirectory = folder;
                        }
                    }
                }

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    textBox.Text = ofd.FileName;
                }
            }
        }

        /// <summary>
        /// Select a text file using an OpenFileDialog
        /// </summary>
        /// <param name="textBox">
        /// The text box.
        /// </param>
        /// <param name="title">
        /// The title.
        /// </param>
        internal static void SelectTextfile(Control textBox, string title)
        {
            using (
                var ofd = new OpenFileDialog
                              {
                                  CheckFileExists = true, 
                                  Filter = @"Text file (*.txt)|*.txt|All files|*.*", 
                                  Multiselect = false, 
                                  SupportMultiDottedExtensions = true, 
                                  Title = title
                              })
            {
                if (textBox.Text != string.Empty)
                {
                    var folder = Path.GetDirectoryName(textBox.Text);
                    if (folder != null)
                    {
                        if (Directory.Exists(folder))
                        {
                            ofd.InitialDirectory = folder;
                        }
                    }
                }

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    textBox.Text = ofd.FileName;
                }
            }
        }

        /// <summary>
        /// Aggregate shapefile
        /// </summary>
        /// <param name="shapefilename">
        /// The textfileLocation.
        /// </param>
        /// <param name="fieldIndex">
        /// The field index.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True when no errors
        /// </returns>
        private static bool AggregateShapefile(string shapefilename, int fieldIndex, Form1 theForm)
        {
            var retVal = true;

            try
            {
                // Check inputs:
                if (!Helper.CheckShapefileLocation(shapefilename, theForm))
                {
                    return false;
                }

                // Open the sf:
                var sf = Fileformats.OpenShapefile(shapefilename, theForm);
                if (sf == null)
                {
                    theForm.Error(string.Empty, "Opening input shapefile was unsuccessful");
                    return false;
                }

                var globalSettings = new GlobalSettings();
                globalSettings.ResetGdalError();

                theForm.Progress(string.Empty, 0, "Start aggregating " + Path.GetFileName(shapefilename));
                var aggregatedSf = sf.AggregateShapes(false, fieldIndex);
                var info = sf.LastOutputValidation;
                if (info != null)
                {
                    Debug.Print("Shapefile returned: " + (aggregatedSf != null));
                    if (aggregatedSf != null)
                    {
                        var hasInvalidShapes = aggregatedSf.HasInvalidShapes();
                        Debug.Print("Has invalid shapes: " + hasInvalidShapes);
                    }

                    Debug.Print("Operation is valid: " + info.IsValid);
                }

                // Check if the result still contained invalid shapes:
                var correctSf = aggregatedSf;

                // Save result:
                var newFilename = shapefilename.Replace(".shp", "-aggregate.shp");
                Helper.DeleteShapefile(newFilename);
                if (correctSf != null)
                {
                    correctSf.SaveAs(newFilename, theForm);
                    theForm.Progress(string.Empty, 100, "The resulting shapefile has been saved as " + newFilename);

                    Helper.ColorShapes(ref correctSf, 0, tkMapColor.YellowGreen, tkMapColor.RoyalBlue, true);

                    // Load the files:
                    MyAxMap.RemoveAllLayers();
                    MyAxMap.AddLayer(correctSf, true);

                    // Wait to show the map:
                    Application.DoEvents();

                    theForm.Progress(
                        string.Format(
                            "The Aggregate shapefile now has {0} shapes instead of {1} and has {2} rows",
                            correctSf.NumShapes,
                            sf.NumShapes,
                            correctSf.Table.NumRows));
                }

                theForm.Progress(string.Empty, 100, " ");
            }
            catch (Exception exception)
            {
                theForm.Error(string.Empty, "Exception: " + exception.Message);
                retVal = false;
            }

            return retVal;
        }

        /// <summary>
        /// Get some random cells and recalculate the values
        /// </summary>
        /// <param name="rasterA">
        /// The first raster
        /// </param>
        /// <param name="rasterB">
        /// The second raster
        /// </param>
        /// <param name="resultRaster">
        /// The result raster.
        /// </param>
        /// <param name="formula">
        /// The formula.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True when OK
        /// </returns>
        private static bool CheckRasterCalculator(
            string rasterA, 
            string rasterB, 
            string resultRaster, 
            string formula, 
            Form1 theForm)
        {
            var retVal = false;
            var grdA = new Grid { GlobalCallback = theForm };
            var grdB = new Grid { GlobalCallback = theForm };
            var grdC = new Grid { GlobalCallback = theForm };

            var randomColumn = 0;
            var randomRow = 0;

            try
            {
                // Open the rasters:
                if (!grdA.Open(rasterA, GridDataType.UnknownDataType, false, GridFileType.UseExtension, theForm))
                {
                    theForm.Error(
                        string.Empty,
                        "Something went wrong opening the first raster: " + grdA.ErrorMsg[grdA.LastErrorCode]);
                    return false;
                }

                if (!grdB.Open(rasterB, GridDataType.UnknownDataType, false, GridFileType.UseExtension, theForm))
                {
                    theForm.Error(
                        string.Empty,
                        "Something went wrong opening the second raster: " + grdB.ErrorMsg[grdB.LastErrorCode]);
                    return false;
                }

                if (!grdC.Open(resultRaster, GridDataType.UnknownDataType, false, GridFileType.UseExtension, theForm))
                {
                    theForm.Error(
                        string.Empty,
                        "Something went wrong opening the resulting raster: " + grdC.ErrorMsg[grdC.LastErrorCode]);
                    return false;
                }

                // Rasters have the same size and the same number of columns and rows.
                var numColumns = grdA.Header.NumberCols;
                var numRows = grdA.Header.NumberRows;
                Debug.WriteLine("grdA.Filename: " + grdA.Filename);
                Debug.WriteLine("grdA.Header.NodataValue: " + grdA.Header.NodataValue);
                Debug.WriteLine("grdA.Minimum: " + grdA.Minimum);
                Debug.WriteLine("grdA.Maximum: " + grdA.Maximum);
                var nodata = Convert.ToDouble(grdA.Header.NodataValue);
                Debug.WriteLine("nodata: " + nodata);
                var rnd = new Random();

                // read cell:
                var valA = nodata;

                // Check for nodata value:
                while (valA == nodata || valA == 0 || valA < Convert.ToDouble(grdA.Minimum)
                       || valA > Convert.ToDouble(grdA.Maximum))
                {
                    randomColumn = rnd.Next(numColumns);
                    randomRow = rnd.Next(numRows);
                    valA = Convert.ToDouble(grdA.Value[randomColumn, randomRow]);
                }

                Debug.WriteLine("valA: " + valA);

                // Get values from other rasters, make expression and evaluate:
                var valB = Convert.ToDouble(grdB.Value[randomColumn, randomRow]);
                Debug.WriteLine("valB: " + valB);
                var valC = Convert.ToDouble(grdC.Value[randomColumn, randomRow]);
                Debug.WriteLine("valC: " + valC);

                // TODO: Is this the best syntax to do this:
                var expression =
                    formula.Replace("[A]", string.Format("({0})", valA.ToString(CultureInfo.InvariantCulture)))
                        .Replace("[B]", string.Format("({0})", valB.ToString(CultureInfo.InvariantCulture)));
                theForm.Progress(string.Empty, 100, "Checking formula: " + formula);
                var e = new Expression(expression);
                var result = e.Evaluate(); // Returns an object
                if (result == null)
                {
                    theForm.Progress("Warning! Could not check this expression using NCalc: " + e.Error);
                    return false;
                }

                var goodCalculation = Math.Abs(valC - Convert.ToDouble(result)) < 0.001;

                if (goodCalculation)
                {
                    theForm.Progress(
                        string.Empty,
                        100,
                        string.Format("Checking some random values was correct: {0} = {1}", expression, result));
                    retVal = true;
                }
                else
                {
                    var msg = string.Format(
                        "The expression {0} returned {1}. But {2} was expected!",
                        expression,
                        result,
                        valC);
                    theForm.Error(string.Empty, msg);
                }
            }
            catch (InvalidCastException ex)
            {
                // Thanks to the suggestions of Jeen this should no longer happen.
                var msg = string.Format(
                    "InvalidCastException in CheckRasterCalculator: {0}{1}Column {2}, row{3}",
                    ex.Message,
                    Environment.NewLine,
                    randomColumn,
                    randomRow);
                theForm.Error(string.Empty, msg);
            }
            catch (EvaluationException ex)
            {
                theForm.Progress("Warning NCalc evaluation exception: " + ex.Message);
            }
            catch (Exception ex)
            {
                theForm.Error(string.Empty, "Error in CheckRasterCalculator: " + ex.Message);
            }
            finally
            {
                // Close the rasters again:
                grdA.Close();
                grdB.Close();
                grdC.Close();
            }

            return retVal;
        }

        /// <summary>
        /// Clip Grid by Polygon
        /// </summary>
        /// <param name="gridFilename">
        /// The grid filename.
        /// </param>
        /// <param name="shapefilename">
        /// The textfileLocation.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True when no errors
        /// </returns>
        private static bool ClipGridByPolygon(string gridFilename, string shapefilename, Form1 theForm)
        {
            var numErrors = 0;

            try
            {
                // Clear the map:
                MyAxMap.RemoveAllLayers();
                Application.DoEvents();

                // Check inputs:
                if (gridFilename == string.Empty || shapefilename == string.Empty)
                {
                    theForm.Error(string.Empty, "Input parameters are wrong");
                    return false;
                }

                var folder = Path.GetDirectoryName(gridFilename);
                if (folder == null)
                {
                    theForm.Error(string.Empty, "Input parameters are wrong");
                    return false;
                }

                if (!Directory.Exists(folder))
                {
                    theForm.Error(string.Empty, "Output folder doesn't exists: " + folder);
                    return false;
                }

                if (!File.Exists(gridFilename))
                {
                    theForm.Error(string.Empty, "Input grid file doesn't exists");
                    return false;
                }

                if (!File.Exists(shapefilename))
                {
                    theForm.Error(string.Empty, "Input shapefile doesn't exists");
                    return false;
                }

                var resultGrid = Path.Combine(folder, "ClipGridByPolygonTest" + Path.GetExtension(gridFilename));
                Helper.DeleteGridfile(resultGrid);

                var globalSettings = new GlobalSettings();
                globalSettings.ResetGdalError();
                var utils = new Utils { GlobalCallback = theForm };
                var sf = new Shapefile();
                sf.Open(shapefilename, theForm);

                // Get one polygon the clip with:
                var index = new Random().Next(sf.NumShapes - 1);
                var polygon = sf.Shape[index];

                // It returns false even if it is created
                if (!utils.ClipGridWithPolygon(gridFilename, polygon, resultGrid, false))
                {
                    var msg = "Failed to process: " + utils.ErrorMsg[utils.LastErrorCode];
                    if (globalSettings.GdalLastErrorMsg != string.Empty)
                    {
                        msg += Environment.NewLine + "GdalLastErrorMsg: " + globalSettings.GdalLastErrorMsg;
                        numErrors++;
                    }

                    theForm.Error(string.Empty, msg);
                }

                if (File.Exists(resultGrid))
                {
                    theForm.Progress(string.Empty, 100, resultGrid + " was successfully created");

                    // Add the layers:
                    // Fileformats.OpenGridAsLayer(gridFilename, theForm, true);
                    MyAxMap.AddLayer(sf, true);
                    Fileformats.OpenGridAsLayer(resultGrid, theForm, false);

                    Application.DoEvents();
                }
                else
                {
                    theForm.Error(string.Empty, "No grid was created");
                    numErrors++;
                }
            }
            catch (Exception exception)
            {
                theForm.Error(string.Empty, "Exception: " + exception.Message);
                numErrors++;
            }

            return numErrors == 0;
        }

        /// <summary>
        /// Do some zooming
        /// </summary>
        /// <param name="theForm">
        /// The form
        /// </param>
        /// <param name="numOverviews">
        /// The number of overviews, -1 if not an image layer
        /// </param>
        private static void DoSomeZooming(ICallback theForm, int numOverviews)
        {
            // Zoom in several times:
            theForm.Progress(string.Empty, 100, "Zoom in several times");

            // Initial zooms for shapefiles and grids:
            var numZooms = 4;

            if (numOverviews != -1)
            {
                // image layer
                numZooms = numOverviews == 0 ? 2 : 4;
            }

            for (var i = 0; i < numZooms; i++)
            {
                MyAxMap.ZoomIn(0.2);
                Application.DoEvents();
            }

            // Zoom out again:
            theForm.Progress(string.Empty, 100, "Zoom out again");
            for (var i = 0; i < numZooms - 1; i++)
            {
                MyAxMap.ZoomOut(0.2);
                Application.DoEvents();
            }

            // Zoom max extent:
            theForm.Progress(string.Empty, 100, "Zoom max extent");
            MyAxMap.ZoomToMaxExtents();
        }

        /// <summary>
        /// End the stop watch and log the time needed
        /// </summary>
        /// <param name="stopWatch">
        /// The stop watch
        /// </param>
        /// <returns>
        /// The elapsed time
        /// </returns>
        private static long EndStopWatch(ref Stopwatch stopWatch)
        {
            Application.DoEvents();
            stopWatch.Stop();
            return stopWatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Load the tiles for the shapefile
        /// </summary>
        /// <param name="shapefileLocation">
        /// The shapefile location.
        /// </param>
        /// <param name="provId">
        /// The prov id.
        /// </param>
        /// <param name="theForm">
        /// The the form.
        /// </param>
        /// <returns>
        /// True when no errors 
        /// </returns>
        private static bool LoadTiles(string shapefileLocation, int provId, Form1 theForm)
        {
            var waitCount = 0;
            const int MaxWaitCount = 15;
            const int MaxSleepTime = 500;
            var hasErrors = false;

            // Open shapefile:
            var layerHandle = Fileformats.OpenShapefileAsLayer(shapefileLocation, theForm, true);
            MyAxMap.ZoomToLayer(layerHandle);

            theForm.Progress(
                string.Empty, 
                100, 
                string.Format("Map projection: {0} ({1})", MyAxMap.GeoProjection.Name, MyAxMap.GeoProjection.GeogCSName));

            var sf = MyAxMap.get_Shapefile(layerHandle);

            if (sf == null)
            {
                theForm.Error(string.Empty, "Shapefile could not be loaded");
                return false;
            }

            // turn off the symbology to see the tiles  
            theForm.Progress(
                string.Empty, 
                100, 
                string.Format("Layer projection: {0} ({1})", sf.GeoProjection.Name, sf.GeoProjection.GeogCSName));
            sf.DefaultDrawingOptions.FillVisible = false;
            sf.Categories.Clear();
            sf.Labels.Clear();
            sf.Charts.Clear();

            Thread.Sleep(MaxSleepTime);
            Application.DoEvents();

            // Set Tiles provider:
            MyAxMap.Tiles.ProviderId = provId;

            // Save some settings:
            var providerId = MyAxMap.Tiles.Providers.IndexByProviderId[MyAxMap.Tiles.ProviderId];
            var maxZoom = MyAxMap.Tiles.Providers.MaxZoom[providerId];
            var minZoom = MyAxMap.Tiles.Providers.MinZoom[providerId];
            const int StartZoom = 10;
            var numFailed = 0;

            var msg = string.Format(
                "{0} has a min zoom of {1} and  a max zoom of {2}",
                MyAxMap.Tiles.ProviderName,
                minZoom,
                maxZoom);
            theForm.Progress(string.Empty, 0, msg);

            // start from current zoom after zooming to layer; when starting from minimum you won't 
            // return back to the same shapefile because of projection distortions
            // infact probably would better to take cities shapefile, take several random cities and zoom on the in particular
            MyAxMap.ZoomToTileLevel(StartZoom);

            Thread.Sleep(MaxSleepTime);
            Application.DoEvents();

            // Continue when tiles are loaded:
            while (!tilesAreLoaded && waitCount < MaxWaitCount)
            {
                Thread.Sleep(MaxSleepTime);
                Application.DoEvents();
                waitCount++;
            }

            if (!tilesAreLoaded)
            {
                theForm.Error(string.Empty, "No tiles have been loaded");
                hasErrors = true;
            }

            // Do some zooming:
            for (var zoom = StartZoom; zoom <= maxZoom - 2; zoom++)
            {
                theForm.Progress(string.Empty, 100, "Zooming to: " + zoom);
                MyAxMap.ZoomToTileLevel(zoom);

                // Continue when tiles are loaded:
                waitCount = 0;

                while (!tilesAreLoaded && waitCount < MaxWaitCount)
                {
                    Thread.Sleep(MaxSleepTime);
                    Application.DoEvents();
                    waitCount++;
                }

                if (!tilesAreLoaded)
                {
                    theForm.Error(string.Empty, "Failed to load tiles for zoom: " + zoom);
                    hasErrors = true;
                    numFailed++;
                }

                tilesAreLoaded = false;

                if (numFailed > 2)
                {
                    // Tried several times, without luck. Stop trying:
                    return false;
                }
            }

            return !hasErrors;
        }

        /// <summary>
        /// TilesLoaded event handle
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private static void MyAxMapTilesLoaded(object sender, _DMapEvents_TilesLoadedEvent e)
        {
            tilesAreLoaded = true;
        }

        /// <summary>
        /// The raster calculator test
        /// </summary>
        /// <param name="rasterA">
        /// The first raster
        /// </param>
        /// <param name="rasterB">
        /// The second raster
        /// </param>
        /// <param name="formula">
        /// The formula
        /// </param>
        /// <param name="theForm">
        /// The form
        /// </param>
        /// <returns>
        /// True on success
        /// </returns>
        private static bool RasterCalculatorTest(string rasterA, string rasterB, string formula, Form1 theForm)
        {
            bool retVal;

            try
            {
                // Clear the map:
                MyAxMap.Clear();
                Application.DoEvents();

                // Check inputs:
                if (rasterA == string.Empty || rasterB == string.Empty)
                {
                    theForm.Error(string.Empty, "Input parameters are wrong");
                    return false;
                }

                var folder = Path.GetDirectoryName(rasterA);
                if (folder == null)
                {
                    theForm.Error(string.Empty, "Input parameters are wrong");
                    return false;
                }

                if (!Directory.Exists(folder))
                {
                    theForm.Error(string.Empty, "Output folder doesn't exists: " + folder);
                    return false;
                }

                if (!File.Exists(rasterA))
                {
                    theForm.Error(string.Empty, "First raster file doesn't exists");
                    return false;
                }

                if (!File.Exists(rasterB))
                {
                    theForm.Error(string.Empty, "Second raster file doesn't exists");
                    return false;
                }

                // var resultRaster = Path.Combine(folder, "RasterCalculatorTest.tif");
                var resultRaster = Helper.CreateOutputFilename(rasterA, "calculated");
                Helper.DeleteGridfile(resultRaster);

                var globalSettings = new GlobalSettings();
                globalSettings.ResetGdalError();
                var utils = new Utils { GlobalCallback = theForm };

                string[] names = { rasterA, rasterB };
                var expression =
                    formula.Replace("[A]", string.Format("[{0}@1]", Path.GetFileName(rasterA)))
                        .Replace("[B]", string.Format("[{0}@1]", Path.GetFileName(rasterB)));

                string errorMsg;
                var stopWatch = Stopwatch.StartNew();
                retVal = utils.CalculateRaster(
                    names, 
                    expression, 
                    resultRaster, 
                    "GTiff", 
                    0f /* no data value */, 
                    theForm /*callback */, 
                    out errorMsg);
                var selectTime = EndStopWatch(ref stopWatch);
                theForm.Progress(
                    string.Empty, 
                    100, 
                    string.Format("The calculation of {0} took {1} seconds", formula, selectTime / 1000.0));

                if (!retVal)
                {
                    var msg = "Failed to process: " + utils.ErrorMsg[utils.LastErrorCode];
                    if (globalSettings.GdalLastErrorMsg != string.Empty)
                    {
                        msg += Environment.NewLine + "GdalLastErrorMsg: " + globalSettings.GdalLastErrorMsg;
                    }

                    if (errorMsg != string.Empty)
                    {
                        msg += Environment.NewLine + "CalculateRaster error: " + errorMsg;
                    }

                    theForm.Error(string.Empty, msg);
                }

                if (retVal && File.Exists(resultRaster))
                {
                    theForm.Progress(string.Empty, 100, resultRaster + " was successfully created");

                    // Do some checking:
                    retVal = CheckRasterCalculator(rasterA, rasterB, resultRaster, formula, theForm);
                    if (retVal)
                    {
                        theForm.Progress(string.Empty, 100, "The random check was successful");
                    }

                    // Add the layer:
                    theForm.Progress(string.Empty, 100, "Adding the calculated raster.");
                    Fileformats.AddLayer(resultRaster, theForm);

                    Application.DoEvents();
                }
                else
                {
                    theForm.Error(string.Empty, "No grid was created");
                }
            }
            catch (Exception exception)
            {
                theForm.Error(string.Empty, "Exception: " + exception.Message);
                retVal = false;
            }

            return retVal;
        }

        /// <summary>
        /// Test the spatial indexing
        /// </summary>
        /// <param name="filename">
        /// The filename.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True on success 
        /// </returns>
        private static bool SpatialIndexTest(string filename, Form1 theForm)
        {
            // Check if file exists:
            if (!Helper.CheckShapefileLocation(filename, theForm))
            {
                return false;
            }

            // Clear map:
            MyAxMap.Clear();
            MyAxMap.Tiles.Visible = false;

            // Remove index files:
            if (filename == null)
            {
                return true;
            }

            var path = Path.GetDirectoryName(filename);
            if (path == null)
            {
                return true;
            }

            var baseFilename = Path.Combine(path, Path.GetFileNameWithoutExtension(filename));
            File.Delete(baseFilename + ".mwd");
            File.Delete(baseFilename + ".dat");
            File.Delete(baseFilename + ".mwx");
            File.Delete(baseFilename + ".idx");

            // Open shapefile:
            var sf = Fileformats.OpenShapefile(filename, theForm);

            if (sf == null)
            {
                theForm.WriteError("Cannot load shapefile");
                return false;
            }

            // Add to map:
            if (MyAxMap.AddLayer(sf, true) == -1)
            {
                theForm.WriteError("Cannot add shapefile to the map");
                return false;
            }

            // Wait to show the map:
            Application.DoEvents();

            // Check:
            theForm.Progress(string.Empty, 100, "Shapefile has index: " + sf.HasSpatialIndex);

            // Now do some selecting to time without spatial index.
            var utils = new Utils { GlobalCallback = theForm };
            sf.SelectionColor = utils.ColorByName(tkMapColor.Yellow);
            var timeWithoutIndex = TimeSelectShapes(ref sf, theForm);

            // for debugging:
            Application.DoEvents();
            Thread.Sleep(1000);

            // Create index:
            if (!sf.CreateSpatialIndex(sf.Filename))
            {
                theForm.Error(string.Empty, "Error creating spatial index: " + sf.ErrorMsg[sf.LastErrorCode]);
                return false;
            }

            // Check:
            theForm.Progress(string.Empty, 100, "SpatialIndexMaxAreaPercent: " + sf.SpatialIndexMaxAreaPercent);

            // Set index:
            sf.UseSpatialIndex = true;

            // Check:
            theForm.Progress(string.Empty, 100, "Shapefile has index: " + sf.HasSpatialIndex);

            // Check if the files are created:
            if (!(File.Exists(baseFilename + ".mwd") || File.Exists(baseFilename + ".dat")))
            {
                theForm.Error(string.Empty, "The mwd file does not exists");
            }

            if (!(File.Exists(baseFilename + ".mwx") || File.Exists(baseFilename + ".idx")))
            {
                theForm.Error(string.Empty, "The mwx file does not exists");
                return false;
            }

            // Now do some selecting to time with spatial index.
            sf.SelectionColor = utils.ColorByName(tkMapColor.Red);
            var timeWithIndex = TimeSelectShapes(ref sf, theForm);

            var msg =
                string.Format(
                    "Select shapes without spatial index took {0} seconds, with spatial index it took {1}",
                    timeWithoutIndex / 1000.0,
                    timeWithIndex / 1000.0);
            theForm.Progress(string.Empty, 0, msg);
            return true;
        }

        /// <summary>
        /// Time the select shapes method
        /// </summary>
        /// <param name="sf">
        /// The shapefile
        /// </param>
        /// <param name="theForm">
        /// The form
        /// </param>
        /// <returns>
        /// The needed time
        /// </returns>
        private static long TimeSelectShapes(ref Shapefile sf, ICallback theForm)
        {
            var boundBox = new Extents();
            var width = sf.Extents.xMax - sf.Extents.xMin;
            var height = sf.Extents.yMax - sf.Extents.yMin;

            sf.SelectNone();
            Application.DoEvents();
            Thread.Sleep(100);

            boundBox.SetBounds(
                sf.Extents.xMin + (width / 3), 
                sf.Extents.yMin + (height / 3), 
                0.0, 
                sf.Extents.xMax - (width / 3), 
                sf.Extents.yMax - (height / 3), 
                0.0);
            object result = null;

            // Start stopwatch:
            var stopWatch = Stopwatch.StartNew();
            var returnCode = sf.SelectShapes(boundBox, 0, SelectMode.INTERSECTION, ref result);
            var selectTime = EndStopWatch(ref stopWatch);

            // Start selecting:
            if (!returnCode)
            {
                theForm.Error(string.Empty, "Error in SelectShapes: " + sf.ErrorMsg[sf.LastErrorCode]);
            }
            else
            {
                var shapes = result as int[];
                if (shapes != null)
                {
                    theForm.Progress(string.Empty, 100, "Number of shapes found: " + shapes.Length);
                    foreach (var index in shapes)
                    {
                        sf.ShapeSelected[index] = true;
                    }

                    Application.DoEvents();
                    MyAxMap.Redraw();
                }
                else
                {
                    theForm.Error(string.Empty, "No shapes found");
                }
            }

            return selectTime;
        }

        /// <summary>
        /// Shapefiles to drawing layer.
        /// </summary>
        /// <param name="shapefilenames">
        /// The shapefilenames.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True on success
        /// </returns>
        private static bool ShapefilesToDrawingLayer(IList<string> shapefilenames, Form1 theForm)
        {
            // Init:
            MyAxMap.Projection = tkMapProjection.PROJECTION_NONE;
            MyAxMap.GrabProjectionFromData = true;
            MyAxMap.LockWindow(tkLockMode.lmLock);
            Extents extents = null;

            try
            {
                // Skip last filename
                for (var s = 0; s < shapefilenames.Count() - 1; s++)
                {
                    var shapefilename = shapefilenames[s];
                    var sf = Fileformats.OpenShapefile(shapefilename, theForm);
                    if (sf == null)
                    {
                        continue;
                    }

                    if (MyAxMap.Projection == tkMapProjection.PROJECTION_NONE)
                    {
                        MyAxMap.GeoProjection = sf.GeoProjection.Clone();
                    }

                    if (extents == null)
                    {
                        extents = sf.Extents; // the extents of the fist shapefile will be used to setup display
                    }

                    var drawHandle = MyAxMap.NewDrawing(tkDrawReferenceList.dlSpatiallyReferencedList);
                    for (var i = 0; i < sf.NumShapes; i++)
                    {
                        var shp = sf.Shape[i];

                        if (shp.ShapeType == ShpfileType.SHP_POINT)
                        {
                            var x = 0.0;
                            var y = 0.0;
                            if (shp.XY[0, ref x, ref y])
                            {
                                MyAxMap.DrawPointEx(drawHandle, x, y, 5, 0);
                            }
                        }
                        else
                        {
                            for (var p = 0; p < shp.NumParts; p++)
                            {
                                var initIndex = shp.Part[p];
                                var numPoints = shp.EndOfPart[p] - shp.Part[p] + 1;
                                if (numPoints <= 0)
                                {
                                    continue;
                                }

                                var x = new double[numPoints];
                                var y = new double[numPoints];

                                for (var j = 0; j < numPoints; j++)
                                {
                                    var valueX = 0.0;
                                    var valueY = 0.0;
                                    if (shp.XY[j + initIndex, ref valueX, ref valueY])
                                    {
                                        x[j] = valueX;
                                        y[j] = valueY;
                                    }
                                }

                                object pointsX = x;
                                object pointsY = y;
                                var drawFill = shp.ShapeType == ShpfileType.SHP_POLYGON;
                                var color = sf.ShapefileType == ShpfileType.SHP_POLYGON
                                                ? sf.DefaultDrawingOptions.FillColor
                                                : sf.DefaultDrawingOptions.LineColor;
                                MyAxMap.DrawPolygonEx(drawHandle, ref pointsX, ref pointsY, numPoints, color, drawFill);
                            }
                        }
                    }
                }
            }
            finally
            {
                if (extents != null)
                {
                    MyAxMap.Extents = extents;
                }

                MyAxMap.LockWindow(tkLockMode.lmUnlock);
                MyAxMap.Redraw2(tkRedrawType.RedrawMinimal);
            }

            return true;
        }

        /// <summary>
        /// The add drawing items.
        /// </summary>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// true on success
        /// </returns>
        private static bool AddDrawingItems(Form1 theForm)
        {
            // Create a drawing layer:
            var drawHandle = MyAxMap.NewDrawing(tkDrawReferenceList.dlSpatiallyReferencedList);

            // init:
            int randomX1;
            int randomY1;
            int randomX2;
            int randomY2;
            int count;
            object pointsX;
            object pointsY;

            // For colors:
            var utils = new UtilsClass();

            // Draw circle:
            GetRandomPoint(out randomX1, out randomY1);
            MyAxMap.DrawCircleEx(drawHandle, randomX1, randomY1, 1000, utils.ColorByName(tkMapColor.Aquamarine), true);
            theForm.Progress(string.Format("The circle is drawn at {0}, {1}", randomX1, randomY1));

            // Draw line
            GetRandomPoint(out randomX1, out randomY1);
            GetRandomPoint(out randomX2, out randomY2);
            MyAxMap.DrawLineEx(drawHandle, randomX1, randomY1, randomX2, randomY2, 10, utils.ColorByName(tkMapColor.BlueViolet));
            theForm.Progress("The line is drawn");

            // Draw point:
            GetRandomPoint(out randomX1, out randomY1);
            MyAxMap.DrawPointEx(drawHandle, randomX1, randomY1, 20, utils.ColorByName(tkMapColor.Bisque));
            theForm.Progress("The point is drawn");

            // Draw polygon:
            GetRandomPoints(out pointsX, out pointsY, out count);
            MyAxMap.DrawPolygonEx(
                drawHandle,
                ref pointsX,
                ref pointsY,
                count,
                utils.ColorByName(tkMapColor.DarkGreen),
                true);
            theForm.Progress("The polygon is drawn");

            // Draw wide circle:
            GetRandomPoint(out randomX1, out randomY1);
            MyAxMap.DrawWideCircleEx(drawHandle, randomX1, randomY1, 500, utils.ColorByName(tkMapColor.DarkRed), false, 5);
            theForm.Progress("The wide circle is drawn");

            // Draw wide polygon
            GetRandomPoints(out pointsX, out pointsY, out count);
            MyAxMap.DrawWidePolygon(
                ref pointsX,
                ref pointsY,
                count,
                utils.ColorByName(tkMapColor.YellowGreen),
                false,
                5);
            theForm.Progress("The wide polygon is drawn");

            // Draw label:
            GetRandomPoint(out randomX1, out randomY1);
            MyAxMap.DrawLabel("My Label", randomX1, randomY1, 45);
            var labels = MyAxMap.get_DrawingLabels(drawHandle);
            if (labels != null)
            {
                labels.FontColor = utils.ColorByName(tkMapColor.Red);
            }
            theForm.Progress("The label is drawn");

            // Redraw just the drawing layers:
            MyAxMap.Redraw2(tkRedrawType.RedrawSkipDataLayers);
            return true;
        }

        /// <summary>
        /// The get random points.
        /// </summary>
        /// <param name="pointsX">
        /// The points x.
        /// </param>
        /// <param name="pointsY">
        /// The points y.
        /// </param>
        /// <param name="count">
        /// The count.
        /// </param>
        private static void GetRandomPoints(out object pointsX, out object pointsY, out int count)
        {
            int randomX1;
            int randomY1;
            int randomX2;
            int randomY2;

            var x = new List<double>();
            var y = new List<double>();

            GetRandomPoint(out randomX1, out randomY1);
            GetRandomPoint(out randomX2, out randomY2);

            x.Add(randomX1);
            y.Add(randomY1);
            x.Add(randomX2);
            y.Add(randomY1);
            x.Add(randomX2);
            y.Add(randomY2);
            x.Add(randomX1);
            y.Add(randomY2);
            x.Add(randomX1);
            y.Add(randomY1);

            pointsX = x.ToArray();
            pointsY = y.ToArray();
            count = x.Count;
        }

        /// <summary>
        /// The get random point.
        /// </summary>
        /// <param name="randomX1">
        /// The random x 1.
        /// </param>
        /// <param name="randomY1">
        /// The random y 1.
        /// </param>
        private static void GetRandomPoint(out int randomX1, out int randomY1)
        {
            var extents = (Extents)MyAxMap.Extents;
            var minX = (int)Math.Round(extents.xMin);
            var maxX = (int)Math.Round(extents.xMax);
            var minY = (int)Math.Round(extents.yMin);
            var maxY = (int)Math.Round(extents.yMax);

            // Get some random locations:
            randomX1 = RandomGenerator.Next(minX, maxX);
            randomY1 = RandomGenerator.Next(minY, maxY);
        }

        #endregion
    }
}