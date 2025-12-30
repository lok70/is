using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using AForge.Imaging;
using AForge.Imaging.Filters;

namespace NeuralNetwork1
{
    public static class ImageProcessor
    {
        public const int InputSide = 32;
        public const int InputSize = InputSide * InputSide;

        // Ползунок 1: порог бинаризации 0..255
        public static int PixelThreshold = 120;

        // Ползунок 2: заливка дыр (макс площадь дырки в пикселях)
        // 0 = выключено
        public static int FillHolesMaxArea = 0;

        public static double[] ProcessImage(Bitmap originalBitmap)
        {
            using (Bitmap processed = GetProcessedBitmap(originalBitmap))
            {
                if (processed == null) return new double[InputSize];

                double[] inputVector = new double[InputSize];
                for (int y = 0; y < InputSide; y++)
                {
                    for (int x = 0; x < InputSide; x++)
                    {
                        // processed уже строго ч/б (0 или 255)
                        Color c = processed.GetPixel(x, y);
                        inputVector[y * InputSide + x] = (c.R > 127) ? 1.0 : 0.0;
                    }
                }
                return inputVector;
            }
        }

        public static Bitmap GetProcessedBitmap(Bitmap originalBitmap)
        {
            if (originalBitmap == null) return null;

            // 1) В 8bpp серый
            Grayscale grayFilter = new Grayscale(0.2125, 0.7154, 0.0721);
            using (Bitmap grayImage = grayFilter.Apply(originalBitmap))
            {
                // 2) Адаптивная бинаризация (Bradley) на 8bpp — нормально
                BradleyLocalThresholding bradley = new BradleyLocalThresholding
                {
                    WindowSize = 80,
                    PixelBrightnessDifferenceLimit = 0.15f
                };
                bradley.ApplyInPlace(grayImage);

                // 3) Инверсия: объект белый на чёрном фоне
                Invert inv = new Invert();
                inv.ApplyInPlace(grayImage);

                // 4) Чуть утолщим штрих (помогает камере)
                Dilatation dil = new Dilatation();
                dil.ApplyInPlace(grayImage);

                // 5) Выделяем основной объект (blob) и кропаем
                BlobCounter bc = new BlobCounter
                {
                    FilterBlobs = true,
                    MinHeight = 5,
                    MinWidth = 5
                };
                bc.ProcessImage(grayImage);

                Rectangle[] rects = bc.GetObjectsRectangles();
                Bitmap target = null;

                if (rects.Length > 0)
                {
                    double maxArea = 0;
                    foreach (var r in rects)
                        maxArea = Math.Max(maxArea, r.Width * r.Height);

                    int minX = int.MaxValue, minY = int.MaxValue;
                    int maxX = int.MinValue, maxY = int.MinValue;
                    bool found = false;

                    foreach (var r in rects)
                    {
                        double area = r.Width * r.Height;
                        if (area < maxArea * 0.15) continue;

                        minX = Math.Min(minX, r.X);
                        minY = Math.Min(minY, r.Y);
                        maxX = Math.Max(maxX, r.Right);
                        maxY = Math.Max(maxY, r.Bottom);
                        found = true;
                    }

                    if (found && (maxX - minX) < grayImage.Width * 0.98)
                    {
                        int pad = 5;
                        int x = Math.Max(0, minX - pad);
                        int y = Math.Max(0, minY - pad);
                        int w = Math.Min(grayImage.Width - x, (maxX - minX) + pad * 2);
                        int h = Math.Min(grayImage.Height - y, (maxY - minY) + pad * 2);

                        Crop crop = new Crop(new Rectangle(x, y, w, h));
                        target = crop.Apply(grayImage);
                    }
                }

                if (target == null)
                {
                    // пустая 64×64
                    return new Bitmap(InputSide, InputSide, PixelFormat.Format24bppRgb);
                }

                Bitmap squared = MakeSquareAndFix(target, InputSide);
                target.Dispose();
                return squared;
            }
        }

        private static Bitmap MakeSquareAndFix(Bitmap bmp, int size)
        {
            // Делаем 24bpp картинку size×size (так проще выводить в PictureBox),
            // а затем бинаризуем и заливаем дырки вручную (без AForge-фильтров на 24bpp).
            Bitmap result = new Bitmap(size, size, PixelFormat.Format24bppRgb);

            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(Color.Black);

                // КЛЮЧЕВО: NearestNeighbor — чтобы "точка" не превращалась в "кольцо" из-за сглаживания
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

                float ratio = Math.Min((float)(size - 4) / bmp.Width, (float)(size - 4) / bmp.Height);
                int newW = Math.Max(1, (int)(bmp.Width * ratio));
                int newH = Math.Max(1, (int)(bmp.Height * ratio));

                int posX = (size - newW) / 2;
                int posY = (size - newH) / 2;

                g.DrawImage(bmp, posX, posY, newW, newH);
            }

            // 1) Ручная бинаризация по PixelThreshold
            // 2) Ручная заливка дыр (если включено)
            BinarizeAndFillHolesInPlace(result, PixelThreshold, FillHolesMaxArea);

            return result;
        }

        private static void BinarizeAndFillHolesInPlace(Bitmap bmp, int threshold, int maxHoleArea)
        {
            int w = bmp.Width;
            int h = bmp.Height;

            // white[x,y] = пиксель объекта (белый)
            bool[,] white = new bool[w, h];

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int v = bmp.GetPixel(x, y).R;
                    white[x, y] = (v >= threshold);
                }

            if (maxHoleArea > 0)
            {
                // visitedBlack = чёрные пиксели, которые относятся к фону (достижимы с границы)
                bool[,] visitedBlack = new bool[w, h];
                var q = new Queue<(int x, int y)>();

                void EnqueueIfBorderBlack(int x, int y)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) return;
                    if (white[x, y]) return;          // белый => не фон
                    if (visitedBlack[x, y]) return;
                    visitedBlack[x, y] = true;
                    q.Enqueue((x, y));
                }

                // стартуем с границ
                for (int x = 0; x < w; x++)
                {
                    EnqueueIfBorderBlack(x, 0);
                    EnqueueIfBorderBlack(x, h - 1);
                }
                for (int y = 0; y < h; y++)
                {
                    EnqueueIfBorderBlack(0, y);
                    EnqueueIfBorderBlack(w - 1, y);
                }

                // BFS по фону
                int[] dx = { 1, -1, 0, 0 };
                int[] dy = { 0, 0, 1, -1 };

                while (q.Count > 0)
                {
                    var (cx, cy) = q.Dequeue();
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = cx + dx[k];
                        int ny = cy + dy[k];
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        if (white[nx, ny]) continue;
                        if (visitedBlack[nx, ny]) continue;
                        visitedBlack[nx, ny] = true;
                        q.Enqueue((nx, ny));
                    }
                }

                // Теперь любые чёрные пиксели, которые НЕ visitedBlack — это дырки внутри объекта.
                bool[,] visitedHole = new bool[w, h];

                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (white[x, y]) continue;              // белое — не дырка
                        if (visitedBlack[x, y]) continue;       // фон — не дырка
                        if (visitedHole[x, y]) continue;

                        // Собираем компоненту дырки
                        var comp = new List<(int x, int y)>();
                        var qq = new Queue<(int x, int y)>();
                        visitedHole[x, y] = true;
                        qq.Enqueue((x, y));

                        while (qq.Count > 0)
                        {
                            var (cx, cy) = qq.Dequeue();
                            comp.Add((cx, cy));

                            for (int k = 0; k < 4; k++)
                            {
                                int nx = cx + dx[k];
                                int ny = cy + dy[k];
                                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                                if (white[nx, ny]) continue;
                                if (visitedBlack[nx, ny]) continue;
                                if (visitedHole[nx, ny]) continue;
                                visitedHole[nx, ny] = true;
                                qq.Enqueue((nx, ny));
                            }
                        }

                        // Если дырка маленькая — заливаем белым
                        if (comp.Count <= maxHoleArea)
                        {
                            foreach (var (px, py) in comp)
                                white[px, py] = true;
                        }
                    }
            }

            // Записываем назад в bitmap строго ч/б
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    bmp.SetPixel(x, y, white[x, y] ? Color.White : Color.Black);
        }
    }
}
