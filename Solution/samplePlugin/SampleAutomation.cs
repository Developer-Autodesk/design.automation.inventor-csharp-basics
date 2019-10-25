/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Path = System.IO.Path;

using Inventor;
using Newtonsoft.Json;
using File = System.IO.File;
using Autodesk.Forge.DesignAutomation.Inventor.Utils;

namespace samplePlugin
{
    [ComVisible(true)]
    public class SampleAutomation
    {
        InventorServer inventorApplication;

        public SampleAutomation(InventorServer inventorApp)
        {
            inventorApplication = inventorApp;
        }

        public void Run(Document doc)
        {
            LogTrace("Run called with {0}", doc.DisplayName);
            File.AppendAllText("output.txt", "Document name: " + doc.DisplayName);
        }

        public void RunWithArguments(Document doc, NameValueMap map)
        {
            // write diagnostics data
            LogInputData(doc, map);

            var pathName = doc.FullFileName;
            LogTrace("Processing " + pathName);

            string[] outputFileName = { "", "ResultSmall", "ResultLarge" };

            try
            {
                var documentType = doc.DocumentType;
                var iRuns = 1;
                if (documentType == DocumentTypeEnum.kPartDocumentObject)
                    iRuns = 2;
                for (int iRun = 1; iRun <= iRuns; iRun++)
                {
                    // load processing parameters
                    string paramsJson = GetParametersToChange(map, iRun);

                    // update parameters in the doc
                    // start HeartBeat around ChangeParameters, it could be a long operation
                    using (new HeartBeat())
                    {
                        ChangeParameters(doc, paramsJson);
                    }

                    var docDir = Path.GetDirectoryName(doc.FullFileName);

                    if (documentType == DocumentTypeEnum.kPartDocumentObject)
                    {
                        PartDocument part = (PartDocument)doc;
                        double mass = part.ComponentDefinition.MassProperties.Mass;
                        string imageParamName = "ImageLight";

                        // check the mass of the document and download proper image
                        if (mass > 300.0)
                            imageParamName = "ImageHeavy";

                        // if debug the samplePlugin locally, the images are already in the inputfiles folder.
                        string imageFileName = $"{outputFileName[iRun]}.png";
                        if (!File.Exists(Path.Combine(docDir, imageFileName)))
                        {
                            // get Image from the OnDemand parameter
                            LogTrace($"Download image '{imageFileName}' from the OnDemand parameter. ");
                            if (!OnDemand.HttpOperation(imageParamName, "", null, $"file://{imageFileName}"))
                            {
                                LogError($"The onDemand operation to download the image '{imageFileName}' failed!");
                            }
                        }
                    }

                    // generate outputs
                    if (documentType == DocumentTypeEnum.kPartDocumentObject)
                    {
                        var fileName = Path.Combine(docDir, $"{outputFileName[iRun]}.ipt"); // the name must be in sync with OutputIpt localName in Activity
                        LogTrace("Saving " + fileName);
                        // start HeartBeat around Save, it could be a long operation
                        using (new HeartBeat())
                        {
                            doc.SaveAs(fileName, false);
                        }
                        LogTrace("Saved as " + fileName);

                        // save an image
                        SaveImageFromPart(Path.Combine(docDir, $"{outputFileName[iRun]}.bmp"), doc as PartDocument);
                    }
                    else // Assembly. That's already validated in ChangeParameters
                    {
                        //Generate drawing document with assembly
                        var idwPath = Path.ChangeExtension(Path.Combine(docDir, doc.DisplayName), "idw");
                        LogTrace($"Generate drawing document");
                        SaveAsIDW(idwPath, doc);

                        // cannot ZIP opened assembly, so close it
                        // start HeartBeat around Save, it could be a long operation
                        using (new HeartBeat())
                        {
                            doc.Save2(true);
                        }
                        doc.Close(true);

                        LogTrace("Zipping up updated Assembly.");

                        // assembly lives in own folder under WorkingDir. Get the WorkingDir
                        var workingDir = Path.GetDirectoryName(docDir);
                        var fileName = Path.Combine(workingDir, "Result.zip"); // the name must be in sync with OutputIam localName in Activity

                        if (File.Exists(fileName)) File.Delete(fileName);

                        // start HeartBeat around ZipFile, it could be a long operation
                        using (new HeartBeat())
                        {
                            ZipFile.CreateFromDirectory(Path.GetDirectoryName(pathName), fileName, CompressionLevel.Fastest, false);
                        }

                        LogTrace($"Saved as {fileName}");
                    }
                }
            }
            catch (Exception e)
            {
                LogError("Processing failed. " + e.ToString());
            }
        }

        /// <summary>
        /// Generate drawing document with input model document
        /// </summary>
        /// <param name="filePath">File path for the generated drawing document</param>
        /// <param name="doc">The Inventor document.</param>
        private void SaveAsIDW(string filePath, Document doc)
        {
            LogTrace("Create a new drawing document");
            DrawingDocument drawDoc = (DrawingDocument)inventorApplication.Documents.Add(DocumentTypeEnum.kDrawingDocumentObject);
            Inventor.Sheet sheet;

            LogTrace("Get or create a new drawing sheet");
            try
            {
                if (drawDoc.Sheets.Count > 0)
                {
                    sheet = drawDoc.Sheets[1];
                    sheet.Size = DrawingSheetSizeEnum.kA2DrawingSheetSize;
                }
                else
                {
                    sheet = drawDoc.Sheets.Add(DrawingSheetSizeEnum.kA2DrawingSheetSize);
                }

                TransientGeometry oTG = inventorApplication.TransientGeometry;
                Inventor.Point2d pt = oTG.CreatePoint2d(10, 10);

                LogTrace("Create a base view");
                Inventor.DrawingView dv = sheet.DrawingViews.AddBaseView((_Document)doc, pt, 1, ViewOrientationTypeEnum.kIsoTopLeftViewOrientation, DrawingViewStyleEnum.kShadedDrawingViewStyle, "", null, null);
                LogTrace("Change scale of base drawing view");
                dv.Scale = CalculateViewSize(sheet, dv);

                LogTrace("Create projected view");
                Inventor.Point2d pt2 = oTG.CreatePoint2d(dv.Position.X, dv.Position.Y + dv.Height * 1.2);
                Inventor.DrawingView projView = sheet.DrawingViews.AddProjectedView(dv, pt2, DrawingViewStyleEnum.kShadedDrawingViewStyle);

                Inventor.Point2d pt3 = oTG.CreatePoint2d(sheet.Width - 5, sheet.Height / 3);
                LogTrace("Create part list");
                Inventor.PartsList pl = sheet.PartsLists.Add(dv, pt3, PartsListLevelEnum.kPartsOnly);

                Inventor.Point2d pt4 = oTG.CreatePoint2d(sheet.Width / 2, sheet.Height / 4);
                LogTrace("Create Revision table");
                Inventor.RevisionTable rtable = sheet.RevisionTables.Add(pt4);
                rtable.ShowTitle = true;
                rtable.Title = "Revision Table Test";
                rtable.RevisionTableRows[1][1].Text = "Inventor IO";
                rtable.RevisionTableRows[1][3].Text = "Test revision table in drawing";
                rtable.RevisionTableRows[1][4].Text = "Autodesk";
                LogTrace("Done:Create Revision table");

                LogTrace($"Saving IDW {filePath}");
                drawDoc.SaveAs(filePath, false);
                drawDoc.Close();
                LogTrace($"Saved IDW as {filePath}");
            }
            catch(Exception e)
            {
                drawDoc.Close();
                LogError($"Generate IDW fails: {e.Message}");
            }
        }

        /// <summary>
        /// Adjust drawing view scale according to the model size
        /// </summary>
        /// <param name="sheet">Drawing sheet</param>
        /// <param name="dv">Drawing view in the sheet</param>
        /// <returns></returns>
        private double CalculateViewSize(Inventor.Sheet sheet, DrawingView dv)
        {
            double dvtemp = dv.Height > dv.Width ? dv.Height : dv.Width;
            double compareBase = dv.Height > dv.Width ? sheet.Height : sheet.Width;

            if (dvtemp > compareBase / 2)
            {
                dv.Scale = dv.Scale / 1.5;
                return CalculateViewSize(sheet, dv);
            }
            else if (dvtemp<compareBase / 3)
            {
                dv.Scale = dv.Scale* 1.5;
                return CalculateViewSize(sheet, dv);
            }
            else
            {
                return dv.Scale;
            }
        }

        private void SaveImageFromPart(string filePath, PartDocument partDoc)
        {
            LogTrace($"Saving image {filePath}");
            Camera cam = inventorApplication.TransientObjects.CreateCamera();
            cam.SceneObject = partDoc.ComponentDefinition;
            cam.ViewOrientationType = ViewOrientationTypeEnum.kIsoTopRightViewOrientation;
            cam.ApplyWithoutTransition();
            cam.SaveAsBitmap(filePath, 200, 200, Type.Missing, Type.Missing);
            LogTrace($"Saved image as {filePath}");
        }

        /// <summary>
        /// First param "_1" should be the filename of the JSON file containing the parameters and values
        /// </summary>
        /// <returns>
        /// JSON with parameters.
        /// JSON content sample:
        ///   { "SquarePegSize": "0.24 in" }
        /// </returns>
        private static string GetParametersToChange(NameValueMap map, int index)
        {
            string paramFile = (string) map.Value[$"_{index}"];
            string json = File.ReadAllText(paramFile);
            LogTrace("Inventor Parameters JSON: \"" + json + "\"");
            return json;
        }

        /// <summary>
        /// Change parameters in Inventor document.
        /// </summary>
        /// <param name="doc">The Inventor document.</param>
        /// <param name="json">JSON with changed parameters.</param>
        public void ChangeParameters(Document doc, string json)
        {
            var theParams = GetParameters(doc);

            Dictionary<string, string> parameters = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            foreach (KeyValuePair<string, string> entry in parameters)
            {
                var parameterName = entry.Key;
                var expression = entry.Value;

                LogTrace("Parameter to change: {0}:{1}", parameterName, expression);

                try
                {
                    Parameter param = theParams[parameterName];
                    param.Expression = expression;
                }
                catch (Exception e)
                {
                    LogError("Cannot update '{0}' parameter. ({1})", parameterName, e.Message);
                }
            }

            doc.Update();
            doc.Save();

            LogTrace("Doc updated.");
        }

        /// <summary>
        /// Get parameters for the document.
        /// </summary>
        /// <returns>Parameters. Throws exception if parameters are not found.</returns>
        private static Parameters GetParameters(Document doc)
        {
            var docType = doc.DocumentType;
            switch (docType)
            {
                case DocumentTypeEnum.kAssemblyDocumentObject:
                    var asm = doc as AssemblyDocument;
                    return asm.ComponentDefinition.Parameters;

                case DocumentTypeEnum.kPartDocumentObject:
                    var ipt = doc as PartDocument;
                    return ipt.ComponentDefinition.Parameters;

                default:
                    throw new ApplicationException(string.Format("Unexpected document type ({0})", docType));
            }
        }

        /// <summary>
        /// Write info on input data to log.
        /// </summary>
        private static void LogInputData(Document doc, NameValueMap map)
        {
            // dump doc name
            var traceInfo = new StringBuilder("RunWithArguments called with '");
            traceInfo.Append(doc.DisplayName);

            traceInfo.Append("'. Parameters: ");

            // dump input parameters
            // values in map are keyed on _1, _2, etc
            string[] parameterValues = Enumerable
                                        .Range(1, map.Count)
                                        .Select(i => (string)map.Value["_" + i])
                                        .ToArray();
            string values = string.Join(", ", parameterValues);
            traceInfo.Append(values);
            traceInfo.Append(".");

            LogTrace(traceInfo.ToString());
        }

        #region Logging utilities

        /// <summary>
        /// Log message with 'trace' log level.
        /// </summary>
        private static void LogTrace(string format, params object[] args)
        {
            Trace.TraceInformation(format, args);
        }

        /// <summary>
        /// Log message with 'trace' log level.
        /// </summary>
        private static void LogTrace(string message)
        {
            Trace.TraceInformation(message);
        }

        /// <summary>
        /// Log message with 'error' log level.
        /// </summary>
        private static void LogError(string format, params object[] args)
        {
            Trace.TraceError(format, args);
        }

        /// <summary>
        /// Log message with 'error' log level.
        /// </summary>
        private static void LogError(string message)
        {
            Trace.TraceError(message);
        }

        #endregion
    }
}
