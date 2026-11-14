//using Mntone.SvgForXaml;
//using Mntone.SvgForXaml.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace NaturplayCalculator.ViewModel
{
    /// <summary>
    /// SVG资源管理类
    /// </summary>
    public static class SvgManager
    {
        private static readonly string[] controlResourceFileNames;
        private static readonly string[] uiResourceFileNames = new string[]
        {
            "logo.svg",
            "logo_b.svg"
        };

        /// <summary>
        /// 15063以上系统的svg资源
        /// </summary>
        private static Dictionary<string, SvgImageSource> svgSources;
        /// <summary>
        /// 14393以下系统的svg资源
        /// </summary>
        //private static Dictionary<string, SvgDocument> svgDocuments;
        /// <summary>
        /// 是否可以直接使用Image控件载入svg
        /// </summary>
        public static readonly bool CanDisplaySvgDirectly;

        public static Dictionary<string, double> EleWHRatio = new Dictionary<string, double>()
        {
            ["A"] = 0.9,
            ["ans"] = 1.57,
            ["arccos"] = 2.65,
            ["arch"] = 1.903,
            ["arcsin"] = 2.563,
            ["arctan"] = 2.697,
            ["arsh"] = 1.82,
            ["arth"] = 1.793,
            ["C"] = 0.85,
            ["cos"] = 1.363,
            ["cosh"] = 1.95,
            ["cov"] = 1.54,
            ["Dx"] = 0.808,
            ["e"] = 0.413,
            ["Ex"] = 0.76,
            ["i"] = 0.375,
            ["lg"] = 0.813,
            ["ln"] = 0.903,
            ["mod"] = 1.927,
            ["pi"] = 0.628,
            ["rank"] = 0.538,
            ["rho"] = 0.585,
            ["sin"] = 1.273,
            ["sinh"] = 1.84,
            ["tan"] = 1.403,
            ["tanh"] = 1.977,
            ["tanhao"] = 0.308,
        };

        static SvgManager()
        {
            CanDisplaySvgDirectly = ApiInformation.IsTypePresent("Microsoft.UI.Xaml.Media.Imaging.SvgImageSource");

            controlResourceFileNames = new string[EleWHRatio.Count * 2];
            var i = 0;
            foreach (var n in EleWHRatio.Keys)
            {
                controlResourceFileNames[i++] = n + "_b.svg";
                controlResourceFileNames[i++] = n + "_w.svg";
            }

            //if (CanDisplaySvgDirectly)
            //{
                svgSources = new Dictionary<string, SvgImageSource>();
                foreach (var n in controlResourceFileNames)
                    svgSources.Add(n, null);
                foreach (var n in uiResourceFileNames)
                    svgSources.Add(n, null);
            //}
            //else
            //{
            //    svgDocuments = new Dictionary<string, SvgDocument>();
            //    foreach (var n in controlResourceFileNames)
            //        svgDocuments.Add(n, null);
            //    foreach (var n in uiResourceFileNames)
            //        svgDocuments.Add(n, null);
            //}

            loadResourcesAsync();
        }

        /// <summary>
        /// 载入SVG文档
        /// </summary>
        private static void loadResourcesAsync()
        {
            //if (CanDisplaySvgDirectly)
            //{
                if (svgSources != null)
                {
                    foreach (var key in controlResourceFileNames)
                        if (svgSources[key] == null)
                            svgSources[key] = getSource(key);
                    foreach (var key in uiResourceFileNames)
                        if (svgSources[key] == null)
                            svgSources[key] = getSource(key);
                }
            //}
            //else
            //{
            //    if (svgDocuments != null)
            //    {
            //        foreach (var key in controlResourceFileNames)
            //            if (svgDocuments[key] == null)
            //                svgDocuments[key] = await getDocumentAsync(key);
            //        foreach (var key in uiResourceFileNames)
            //            if (svgDocuments[key] == null)
            //                svgDocuments[key] = await getDocumentAsync(key);
            //    }
            //}
        }

        private static SvgImageSource getSource(string fileName)
        {
            return new SvgImageSource(new Uri($"ms-appx:///Svg/{fileName}"));
        }

        //private static async Task<SvgDocument> getDocumentAsync(string fileName)
        //{
        //    var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Svg/{fileName}"));
        //    var stream = await file.OpenStreamForReadAsync();
        //    var bytes = new byte[stream.Length];
        //    await stream.ReadAsync(bytes, 0, bytes.Length);
        //    return SvgDocument.Parse(bytes);
        //}

        /// <summary>
        /// 获取Source对象
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static SvgImageSource GetSource(string fileName)
        {
            if (svgSources.ContainsKey(fileName))
            {
                if (svgSources[fileName] == null)
                    svgSources[fileName] = getSource(fileName);
                return svgSources[fileName];
            }
            else
                throw new Exception();
        }

        /// <summary>
        /// 获取SvgDocument对象
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        //public static async Task<SvgDocument> GetDocumentAsync(string fileName)
        //{
        //    if (svgDocuments.ContainsKey(fileName))
        //    {
        //        if (svgDocuments[fileName] == null)
        //            svgDocuments[fileName] = await getDocumentAsync(fileName);
        //        return svgDocuments[fileName];
        //    }
        //    else
        //        throw new Exception();
        //}

        /// <summary>
        /// 在控件内显示SVG
        /// </summary>
        /// <param name="si"></param>
        //public static async void LoadSvg(SvgImage si)
        //{
        //    var filename = "";
        //    string tag = si.Tag.ToString();
        //    int index = tag.IndexOf('|');
        //    if (index >= 0)
        //        filename = tag.Substring(index + 1);
        //    else
        //        filename = tag;

        //    if (svgDocuments.ContainsKey(filename))
        //    {
        //        if (svgDocuments[filename] == null)
        //            svgDocuments[filename] = await getDocumentAsync(filename);
        //        si.LoadSvg(svgDocuments[filename]);
        //    }
        //    else
        //        throw new Exception();
        //}

        /// <summary>
        /// 在控件内显示SVG
        /// </summary>
        /// <param name="si"></param>
        public static void LoadSvg(Image si)
        {
            var filename = "";
            string tag = si.Tag.ToString();
            int index = tag.IndexOf('|');
            if (index >= 0)
                filename = tag.Substring(index + 1);
            else
                filename = tag;

            if (svgSources.ContainsKey(filename))
            {
                if (svgSources[filename] == null)
                    svgSources[filename] = getSource(filename);
                si.Source = svgSources[filename];
            }
            else
                throw new Exception();
        }

    }
}
