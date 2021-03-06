﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FileManagerTests.cs" company="MapWindow Open Source GIS">
//   MapWindow developers community - 2014
// </copyright>
// <summary>
//   The file manager tests.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace TestApplication
{
    #region

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows.Forms;

    using AxMapWinGIS;

    using MapWinGIS;

    #endregion

    /// <summary>
    /// The file manager tests.
    /// </summary>
    internal static class FileManagerTests
    {
        #region Static Fields

        /// <summary>
        /// The error.
        /// </summary>
        private static bool error;

        #endregion

        #region Properties

        /// <summary>
        ///     Gets or sets Map.
        /// </summary>
        internal static AxMap MyAxMap { get; set; }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// Run the Filemanager Analyze Files test
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
        public static bool RunAnalyzeFilesTest(string textfileLocation, Form1 theForm)
        {
            var numErrors = 0;

            // Open text file:
            if (!File.Exists(textfileLocation))
            {
                throw new FileNotFoundException("Cannot find text file.", textfileLocation);
            }

            theForm.Progress(
                string.Empty, 
                0, 
                "-----------------------The Filemanager Analyze Files tests have started.");

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);
            foreach (var line in lines)
            {
                if (!AnalyzeFiles(line, theForm))
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
                string.Format("The Filemanager Analyze Files tests have finished, with {0} errors", numErrors));

            return numErrors == 0;
        }

        /// <summary>
        /// Run the Filemanager Grid open test
        /// </summary>
        /// <param name="textfileLocation">
        /// The location of the text file.
        /// </param>
        /// <param name="theForm">
        /// The form.
        /// </param>
        /// <returns>
        /// True on success
        /// </returns>
        public static bool RunGridOpenTest(string textfileLocation, Form1 theForm)
        {
            var numErrors = 0;

            // Open text file:
            if (!File.Exists(textfileLocation))
            {
                throw new FileNotFoundException("Cannot find text file.", textfileLocation);
            }

            theForm.Progress(string.Empty, 0, "-----------------------The Filemanager Grid open tests have started." + Environment.NewLine);

            // Read text file:
            var lines = Helper.ReadTextfile(textfileLocation);
            foreach (var line in lines)
            {
                if (!GridOpenTest(line, theForm))
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
                string.Format("The Filemanager Grid open tests have finished, with {0} errors", numErrors));

            return numErrors == 0;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Analyzes raster file, displaying possible open strategies
        /// </summary>
        /// <param name="filename">
        /// The path.
        /// </param>
        /// <param name="theForm">
        /// The the Form.
        /// </param>
        /// <returns>
        /// True on success
        /// </returns>
        private static bool AnalyzeFiles(string filename, Form1 theForm)
        {
            error = false;

            // running check for all files with such extensions
            var manager = new FileManager();

            var count = 0;

            // getting list of extension for images and grids
            var img = new Image();
            var filter = img.CdlgFilter.Replace("*.", string.Empty);
            var dict = filter.Split(new[] { '|' }).ToDictionary(item => item);

            var grid = new Grid();
            filter = grid.CdlgFilter.Replace("*.", string.Empty);
            var dict2 = filter.Split(new[] { '|' }).ToDictionary(item => item);

            dict = dict.Keys.Union(dict2.Keys).ToDictionary(item => item);

            var notSupportedExtensions = new HashSet<string>();

            if (File.Exists(filename))
            {
                var extension = Path.GetExtension(filename);
                if (extension != null)
                {
                    var ext = extension.Substring(1).ToLower();

                    if (dict.ContainsKey(ext))
                    {
                        Write(string.Format("{0}. Filename: {1}", count++, Path.GetFileName(filename)));
                        Write(string.Format("Is supported: {0}", manager.IsSupported[filename]));
                        Write(string.Format("Is RGB image: {0}", manager.IsRgbImage[filename]));
                        Write(string.Format("Is grid: {0}", manager.IsGrid[filename]));
                        Write(string.Format("DEFAULT OPEN STRATEGY: {0}", manager.OpenStrategy[filename]));
                        Write(
                            string.Format(
                                "Can open as RGB image: {0}", 
                                manager.CanOpenAs[filename, tkFileOpenStrategy.fosRgbImage]));
                        Write(
                            string.Format(
                                "Can open as direct grid: {0}", 
                                manager.CanOpenAs[filename, tkFileOpenStrategy.fosDirectGrid]));
                        Write(
                            string.Format(
                                "Can open as proxy grid: {0}", 
                                manager.CanOpenAs[filename, tkFileOpenStrategy.fosProxyForGrid]));
                        Write(string.Format("------------------------------------------"));

                        Write(string.Format(string.Empty));

                        // TODO: try to open with these strategies
                        var rst = manager.OpenRaster(filename, tkFileOpenStrategy.fosAutoDetect, theForm);
                        if (rst != null)
                        {
                            MyAxMap.Clear();
                            MyAxMap.Tiles.Visible = false;
                            if (MyAxMap.AddLayer(rst, true) == -1)
                            {
                                Error("Cannot add the raster file to the map");
                            }
                        }
                        else
                        {
                            Error("Cannot load the raster file");
                        }
                    }
                    else
                    {
                        if (!notSupportedExtensions.Contains(ext))
                        {
                            notSupportedExtensions.Add(ext);
                        }
                    }
                }
            }
            else
            {
                Error(filename + " does not exists.");
            }

            if (notSupportedExtensions.Any())
            {
                Write("The following extensions, are among common dialog filters:");
                foreach (var extension in notSupportedExtensions.ToList())
                {
                    Write(extension);
                }
            }

            return !error;
        }

        /// <summary>
        /// The error.
        /// </summary>
        /// <param name="msg">
        /// The msg.
        /// </param>
        private static void Error(string msg)
        {
            Form1.Instance.WriteError(msg);
            error = true;
        }

        /// <summary>
        /// Opens grid with different options and checks how the open strategy is chosen
        /// </summary>
        /// <param name="filename">
        /// The filename.
        /// </param>
        /// <param name="theForm">
        /// The Form.
        /// </param>
        /// <returns>
        /// True on success
        /// </returns>
        private static bool GridOpenTest(string filename, ICallback theForm)
        {
            error = false;
            var numErrors = 0;

            if (!File.Exists(filename))
            {
                Error("Filename wasn't found: " + filename);
                return false;
            }

            var gs = new GlobalSettings
                         {
                             GridProxyFormat = tkGridProxyFormat.gpfTiffProxy, 
                             MinOverviewWidth = 512, 
                             RasterOverviewCreation = tkRasterOverviewCreation.rocYes
                         };

            var fm = new FileManager { GlobalCallback = theForm };

            Write("Working with " + filename);

            // first, let's check that overview creation/removal works correctly
            fm.ClearGdalOverviews(filename);
            var overviews = fm.HasGdalOverviews[filename];
            if (overviews)
            {
                Error("Failed to remove overviews.");
                numErrors++;
            }

            fm.BuildGdalOverviews(filename);
            overviews = fm.HasGdalOverviews[filename];
            if (!overviews)
            {
                Error("Failed to build overviews.");
                numErrors++;
            }

            fm.ClearGdalOverviews(filename);
            fm.RemoveProxyForGrid(filename);

            var hasValidProxyForGrid = fm.HasValidProxyForGrid[filename];
            Write("File has valid proxy for grid: " + hasValidProxyForGrid);

            for (var i = 0; i < 6; i++)
            {
                var strategy = tkFileOpenStrategy.fosAutoDetect;
                switch (i)
                {
                    case 0:
                        Write("1. AUTO DETECT BEHAVIOR. OVERVIEWS MUST BE BUILT.");
                        strategy = tkFileOpenStrategy.fosAutoDetect;
                        break;
                    case 1:
                        Write("2. EXPLICIT PROXY MODE. OVERVIEWS MUST BE IGNORED, PROXY CREATED.");
                        strategy = tkFileOpenStrategy.fosProxyForGrid;
                        break;
                    case 2:
                        Write("3. AUTODETECT MODE. OVERVIEWS REMOVED. PROXY MUST BE REUSED.");
                        strategy = tkFileOpenStrategy.fosAutoDetect;
                        fm.ClearGdalOverviews(filename);
                        break;
                    case 3:
                        Write("4. EXPLICIT DIRECT MODE; PROXY MUST BE IGNORED; OVERVIEWS CREATED.");
                        strategy = tkFileOpenStrategy.fosDirectGrid;
                        break;
                    case 4:
                        Write("5. OVERVIEWS CREATION IS OFF; BUT FILE SIZE IS TOO SMALL FOR PROXY.");
                        strategy = tkFileOpenStrategy.fosAutoDetect;
                        fm.RemoveProxyForGrid(filename);
                        fm.ClearGdalOverviews(filename);
                        gs.MaxDirectGridSizeMb = 100;
                        gs.RasterOverviewCreation = tkRasterOverviewCreation.rocNo;
                        break;
                    case 5:
                        Write("6. OVERVIEWS CREATION IS OFF; BUT FILE SIZE IS LARGE ENOUGH FOR PROXY.");
                        strategy = tkFileOpenStrategy.fosAutoDetect;
                        gs.MaxDirectGridSizeMb = 1;
                        break;
                }

                Write("Gdal overviews: " + fm.HasGdalOverviews[filename]);
                Write("Grid proxy: " + fm.HasValidProxyForGrid[filename]);

                var img = fm.OpenRaster(filename, strategy, theForm);
                if (img == null)
                {
                    Error("Could not load raster");
                    numErrors++;
                    continue;
                }

                // Don't add the image to the map, because we're going to delete some helper files:
                img.Close();

                strategy = fm.LastOpenStrategy;
                overviews = fm.HasGdalOverviews[filename];
                hasValidProxyForGrid = fm.HasValidProxyForGrid[filename];
                Write("Last open strategy: " + strategy);
                Write("Gdal overviews: " + overviews);
                Write("Grid proxy: " + hasValidProxyForGrid);

                switch (i)
                {
                    case 0:
                        if (!overviews)
                        {
                            Error("Failed to build overviews.");
                            numErrors++;
                        }

                        if (strategy != tkFileOpenStrategy.fosDirectGrid)
                        {
                            Error("Direct grid strategy was expected.");
                            numErrors++;
                        }

                        break;
                    case 1:
                        if (!hasValidProxyForGrid)
                        {
                            Error("Failed to build proxy.");
                            numErrors++;
                        }

                        if (strategy != tkFileOpenStrategy.fosProxyForGrid)
                        {
                            Error("Proxy strategy was expected.");
                            numErrors++;
                        }

                        break;
                    case 2:
                        if (overviews)
                        {
                            Error("Failed to remove overviews.");
                            numErrors++;
                        }

                        if (strategy != tkFileOpenStrategy.fosProxyForGrid)
                        {
                            Error("Proxy strategy was expected.");
                            numErrors++;
                        }

                        break;
                    case 3:
                        if (strategy != tkFileOpenStrategy.fosDirectGrid)
                        {
                            Error("Direct grid strategy was expected.");
                            numErrors++;
                        }

                        if (!overviews)
                        {
                            Error("Failed to build overviews.");
                            numErrors++;
                        }

                        break;
                    case 4:
                        if (overviews)
                        {
                            Error("No overviews is expected.");
                            numErrors++;
                        }

                        if (strategy != tkFileOpenStrategy.fosDirectGrid)
                        {
                            Error("Direct grid strategy was expected.");
                            numErrors++;
                        }

                        break;
                    case 5:
                        if (!hasValidProxyForGrid)
                        {
                            Error("Failed to build proxy.");
                            numErrors++;
                        }

                        if (strategy != tkFileOpenStrategy.fosProxyForGrid)
                        {
                            Error("Proxy strategy was expected.");
                            numErrors++;
                        }

                        break;
                }
            }

            fm.ClearGdalOverviews(filename);
            fm.RemoveProxyForGrid(filename);

            overviews = fm.HasGdalOverviews[filename];
            hasValidProxyForGrid = fm.HasValidProxyForGrid[filename];

            Write("Gdal overviews: " + fm.HasGdalOverviews[filename]);
            Write("Grid proxy: " + fm.HasValidProxyForGrid[filename]);
            if (hasValidProxyForGrid)
            {
                Error("Failed to remove proxy.");
                numErrors++;
            }

            if (overviews)
            {
                Error("Failed to remove overviews.");
                numErrors++;
            }

            Write(string.Format("This run had {0} errors", numErrors));
            return numErrors == 0;
        }

        /// <summary>
        /// The write.
        /// </summary>
        /// <param name="msg">
        /// The msg.
        /// </param>
        private static void Write(string msg)
        {
            Form1.Instance.WriteMsg(msg);
        }

        #endregion
    }
}