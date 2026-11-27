using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PipelineGenWpf.Models;

namespace PipelineGenWpf.Services
{
    public static class FileHelper
    {
        public static LoadedFileInfo PickFileWithDialog()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "Выберите файл для пайплайна";
            dlg.Filter = "Все поддерживаемые файлы|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.txt;*.md;*.csv;*.json|" +
                         "Изображения (Vision)|*.png;*.jpg;*.jpeg;*.webp;*.bmp|" +
                         "Текстовые файлы|*.txt;*.md;*.csv;*.json|" +
                         "Все файлы|*.*";

            bool? result = dlg.ShowDialog();
            if (result != true || string.IsNullOrEmpty(dlg.FileName))
            {
                return null;
            }

            return LoadFromPath(dlg.FileName);
        }

        public static LoadedFileInfo LoadFromPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            FileInfo fi = new FileInfo(filePath);
            byte[] bytes = File.ReadAllBytes(filePath);
            string ext = (fi.Extension ?? "").ToLowerInvariant();
            bool isImage = ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp" || ext == ".bmp";

            string mimeType;
            if (ext == ".png") mimeType = "image/png";
            else if (ext == ".jpg" || ext == ".jpeg") mimeType = "image/jpeg";
            else if (ext == ".webp") mimeType = "image/webp";
            else if (ext == ".bmp") mimeType = "image/bmp";
            else if (ext == ".json") mimeType = "application/json";
            else if (ext == ".csv") mimeType = "text/csv";
            else mimeType = "text/plain";

            LoadedFileInfo info = new LoadedFileInfo
            {
                FileName = fi.Name,
                FileSize = fi.Length,
                FileType = mimeType,
                IsImage = isImage
            };

            if (isImage)
            {
                string b64 = Convert.ToBase64String(bytes);
                info.FileData = string.Format("data:{0};base64,{1}", mimeType, b64);
                info.Content = string.Format("[Изображение: {0} ({1} KB)]", fi.Name, Math.Max(1, fi.Length / 1024));
            }
            else
            {
                string text = Encoding.UTF8.GetString(bytes);
                info.Content = text;
                info.FileData = text;
            }

            return info;
        }

        public static LoadedFileInfo TryLoadImageFromClipboard()
        {
            try
            {
                if (!Clipboard.ContainsImage())
                {
                    if (Clipboard.ContainsFileDropList())
                    {
                        System.Collections.Specialized.StringCollection files = Clipboard.GetFileDropList();
                        if (files != null && files.Count > 0)
                        {
                            return LoadFromPath(files[0]);
                        }
                    }
                    return null;
                }

                BitmapSource bitmap = Clipboard.GetImage();
                if (bitmap == null)
                {
                    return null;
                }

                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using (MemoryStream ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    byte[] pngBytes = ms.ToArray();
                    string b64 = Convert.ToBase64String(pngBytes);
                    string fileName = string.Format("скриншот_{0}.png", DateTime.Now.ToString("HH-mm-ss"));
                    return new LoadedFileInfo
                    {
                        FileName = fileName,
                        FileSize = pngBytes.Length,
                        FileType = "image/png",
                        IsImage = true,
                        FileData = "data:image/png;base64," + b64,
                        Content = "[Изображение из буфера обмена]"
                    };
                }
            }
            catch
            {
                return null;
            }
        }

        public static BitmapImage DecodeDataUrlImage(string dataUrl)
        {
            if (string.IsNullOrEmpty(dataUrl) || !dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                int commaIdx = dataUrl.IndexOf(',');
                if (commaIdx < 0) return null;
                string b64 = dataUrl.Substring(commaIdx + 1);
                byte[] bytes = Convert.FromBase64String(b64);

                BitmapImage img = new BitmapImage();
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.DecodePixelWidth = 140;
                    img.StreamSource = ms;
                    img.EndInit();
                }
                img.Freeze();
                return img;
            }
            catch
            {
                return null;
            }
        }
    }
}
