using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using AForge.Imaging.Filters;

namespace NeuralNetwork1
{
    // Если FigureType уже есть, удалите enum
    public enum FigureType : byte
    {
        Power = 0, Play, Pause, Stop, Record, Next, Prev, Fwd, Rewind, Eject, Minus, Plus, Undef
    };

    public class GenerateImage
    {
        private Random rand = new Random();
        public int FigureCount { get; set; } = 12;

        private Dictionary<FigureType, List<Bitmap>> _templates = new Dictionary<FigureType, List<Bitmap>>();
        private Bitmap _lastGeneratedBitmap;

        public GenerateImage()
        {
            LoadTemplates();
        }

        public void LoadTemplates()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dataset");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            // Идея: можно положить НЕ один, а несколько рукописных образцов на класс.
            // Поддерживаем файлы вида: Power.png, Power_1.png, Power_2.png ... (любое количество).
            // То же для остальных классов.
            var names = new Dictionary<FigureType, string>
            {
                { FigureType.Power, "Power" }, { FigureType.Play, "Play" },
                { FigureType.Pause, "Pause" }, { FigureType.Stop, "Stop" },
                { FigureType.Record, "Record" }, { FigureType.Next, "Next" },
                { FigureType.Prev, "Prev" }, { FigureType.Fwd, "Fwd" },
                { FigureType.Rewind, "Rewind" }, { FigureType.Eject, "Eject" },
                { FigureType.Minus, "Minus" }, { FigureType.Plus, "Plus" }
            };

            foreach (var lst in _templates.Values)
                foreach (var bmp in lst)
                    bmp.Dispose();
            _templates.Clear();

            foreach (var kvp in names)
            {
                FigureType type = kvp.Key;
                string prefix = kvp.Value;

                var files = Directory
                    .EnumerateFiles(path, prefix + "*.png", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f)
                    .ToList();

                if (files.Count == 0) continue;

                var list = new List<Bitmap>();
                foreach (var fullPath in files)
                {
                    using (Bitmap raw = new Bitmap(fullPath))
                    {
                        Bitmap bmp = new Bitmap(200, 200, PixelFormat.Format24bppRgb);
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.Clear(Color.White);
                            g.DrawImage(raw, 0, 0, 200, 200);
                        }
                        list.Add(bmp);
                    }
                }

                _templates[type] = list;
            }
        }

        public int CountLoadedTemplates() => _templates.Values.Sum(v => v.Count);

        public Sample GenerateFigure()
        {
            FigureType type = (FigureType)rand.Next(FigureCount);

            if (!_templates.TryGetValue(type, out List<Bitmap> templates) || templates.Count == 0)
            {
                // Если шаблонов нет – возвращаем пустой образ (как и раньше)
                return new Sample(new double[ImageProcessor.InputSize], FigureCount, type);
            }

            Bitmap template = templates[rand.Next(templates.Count)];

            // ВАЖНО: генератор должен создавать данные, максимально похожие на то,
            // что приходит с веб-камеры, иначе сеть учится на "одной физике", а распознаёт на другой.
            // Поэтому здесь делаем только геометрические/шумовые искажения, а бинаризацию и кадрирование
            // доверяем ImageProcessor (тот же код используется для камеры).

            Bitmap augmented = new Bitmap(200, 200, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(augmented))
            {
                g.Clear(Color.White);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                // 1) Случайный поворот
                float angle = (float)(rand.NextDouble() * 30.0 - 15.0); // [-15; +15]
                // 2) Случайный масштаб
                float scale = (float)(0.70 + rand.NextDouble() * 0.35); // [0.70; 1.05]
                // 3) Случайный сдвиг (смещение "на листе")
                float shiftX = (float)(rand.NextDouble() * 30.0 - 15.0);
                float shiftY = (float)(rand.NextDouble() * 30.0 - 15.0);

                // Центр трансформаций
                g.TranslateTransform(100 + shiftX, 100 + shiftY);
                g.RotateTransform(angle);
                g.ScaleTransform(scale, scale);
                g.TranslateTransform(-100, -100);

                g.DrawImage(template, 0, 0, 200, 200);

                g.ResetTransform();
            }

            // 4) Толщина линий: иногда чуть утолщаем, иногда чуть "съедаем"
            if (rand.NextDouble() < 0.35)
            {
                Dilatation dil = new Dilatation();
                dil.ApplyInPlace(augmented);
            }
            if (rand.NextDouble() < 0.15)
            {
                Erosion er = new Erosion();
                er.ApplyInPlace(augmented);
            }

            // 5) Небольшой шум (имитация камеры/бумаги)
            if (rand.NextDouble() < 0.50)
            {
                Jitter jitter = new Jitter(2);
                jitter.ApplyInPlace(augmented);
            }

            // 6) Вектор признаков так же, как для камеры
            double[] input = ImageProcessor.ProcessImage(augmented);

            if (_lastGeneratedBitmap != null) _lastGeneratedBitmap.Dispose();
            _lastGeneratedBitmap = augmented; // для отображения на форме

            return new Sample(input, FigureCount, type);
        }

        public Bitmap GenBitmap()
        {
            if (_lastGeneratedBitmap == null)
                return new Bitmap(200, 200, PixelFormat.Format24bppRgb);
            return new Bitmap(_lastGeneratedBitmap);
        }
    }
}