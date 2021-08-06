using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace BakuImage
{
	class Program
	{

		static int ReadBE16(FileStream fs)
		{
			byte[] tmp = new byte[2];
			fs.Read(tmp, 0, 2);
			byte tmp2 = tmp[0];
			tmp[0] = tmp[1];
			tmp[1] = tmp2;
			return BitConverter.ToInt16(tmp, 0);
		}

		static int ReadBE32(FileStream fs)
		{
			byte[] tmp = new byte[4];
			fs.Read(tmp, 0, 4);
			byte tmp2 = tmp[0];
			byte tmp3 = tmp[1];
			tmp[0] = tmp[3];
			tmp[1] = tmp[2];
			tmp[2] = tmp3;
			tmp[3] = tmp2;
			return BitConverter.ToInt32(tmp, 0);
		}

		static float ReadBEF32(FileStream fs)
		{
			byte[] tmp = new byte[4];
			fs.Read(tmp, 0, 4);
			byte tmp2 = tmp[0];
			byte tmp3 = tmp[1];
			tmp[0] = tmp[3];
			tmp[1] = tmp[2];
			tmp[2] = tmp3;
			tmp[3] = tmp2;
			return BitConverter.ToSingle(tmp, 0);
		}

		static int Bit5Convert(int input)
		{
			float col = input;
			col = Math.Min(col / 31.0f, 1) * 255;
			return (int)col;
		}

		static int Bit4Convert(int input)
		{
			float col = input;
			col = Math.Min(col / 15.0f, 1) * 255;
			return (int)col;
		}

		static int Bit3Convert(int input)
		{
			float col = input;
			col = Math.Min(col / 8.0f, 1) * 255;
			return (int)col;
			//return (input << 5);
		}

		static int Bit6Convert(int input)
		{
			float col = input;
			col = Math.Min(col / 63.0f, 1) * 255;
			return (int)col;
		}


		static Color GetSwizzledColor(Color c0, Color c1, int t)
		{
			switch (t)
			{
				case 0:
					return c0;
				case 1:
					return c1;
				case 2:
					return Color.FromArgb(((2 * c0.R) + c1.R) / 3, ((2 * c0.G) + c1.G) / 3, ((2 * c0.B) + c1.B) / 3);
				case 3:
					return Color.FromArgb((c0.R + c1.R) / 2, (c0.G + c1.G) / 2, (c0.B + c1.B) / 2);
			}
			return Color.Black;
		}

		static Color ReadColor(ushort col, int format)
		{
			switch (format)
			{
				case 0x01000100:
					{
						Color c0;
						c0 = Color.FromArgb(Bit5Convert((col & 0xF800) >> 11), Bit6Convert((col & 0x07E0) >> 5), Bit5Convert(col & 0x1F));
						return c0;
					}
				default:
					{
						bool alpha = (col & 0x8000) > 0;
						Color c0;
						if (!alpha)
						{
							c0 = Color.FromArgb(Bit3Convert((col & 0xF000) >> 12), Bit4Convert((col & 0x0F00) >> 8), Bit4Convert((col & 0x00F0) >> 4), Bit4Convert(col & 0xF));
						}
						else
						{
							c0 = Color.FromArgb(Bit5Convert((col & 0x7C00) >> 10), Bit5Convert((col & 0x03E0) >> 5), Bit5Convert(col & 0x1F));
						}
						return c0;
					}
			}
		}
		static void ConvertImages(string file, string path)
		{
			FileStream fs;
			try
			{
				fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
			} catch
			{
				Console.WriteLine("Failed to open file: " + file);
				return;
			}
			// start with how many images there are
			int imageCount = ReadBE16(fs);
			fs.Seek(-0x2, SeekOrigin.Current);
			// iterate over all of them
			for (int i = 0; i < imageCount; ++i)
			{
				// blank
				ReadBE32(fs);
				int dataOffset = ReadBE32(fs);
				int paletteOffset = ReadBE32(fs);
				int height = ReadBE16(fs);
				int width = ReadBE16(fs);
				// unknown
				int unknownBitflag = ReadBE32(fs);
				// unused?
				ReadBE32(fs);
				// format, presumably
				int format = ReadBE32(fs);
				// palette format?
				int colorFormat = ReadBE32(fs);
				long nextFile = fs.Position;
				fs.Seek(dataOffset, SeekOrigin.Begin);

				Bitmap bitmap = new Bitmap(width, height);

				bool swizzleState = false;
				
				// swizzled textures
				if (format == 0xE)
				{
					for (int i2 = 0; i2 < height; i2 += 8) {
						for (int i3 = 0; i3 < width; i3 += 4) {
							// 2 16 bit colors first
							ushort col0 = (ushort)ReadBE16(fs);
							// ??
							Color c0 = Color.FromArgb(Bit5Convert((col0 & 0xF800) >> 11), Bit6Convert((col0 & 0x07E0) >> 5), Bit5Convert(col0 & 0x1F));
							ushort col1 = (ushort)ReadBE16(fs);
							Color c1 = Color.FromArgb(Bit5Convert((col1 & 0xF800) >> 11), Bit6Convert((col1 & 0x07E0) >> 5), Bit5Convert(col1 & 0x1F));

							// iterate over 4 more bytes holding data on the next 16 pixels based off our 2 colors
							for (int i4 = 0; i4 < 4; ++i4)
							{
								byte currByte = (byte)fs.ReadByte();
								int t0 = currByte & 0x3;
								int t1 = (currByte >> 2) & 0x3;
								int t2 = (currByte >> 4) & 0x3;
								int t3 = (currByte >> 6) & 0x3;
								Color tmpCol = GetSwizzledColor(c0, c1, t0);
								bitmap.SetPixel(i3 + 3, i2 + i4, GetSwizzledColor(c0, c1, t0));
								bitmap.SetPixel(i3 + 2, i2 + i4, GetSwizzledColor(c0, c1, t1));
								bitmap.SetPixel(i3 + 1, i2 + i4, GetSwizzledColor(c0, c1, t2));
								bitmap.SetPixel(i3, i2 + i4, GetSwizzledColor(c0, c1, t3));
							}

							// dumb...
							if (i3 % 8 == 4 && !swizzleState)
							{
								i2 += 4;
								i3 -= 8;
								swizzleState = true;
							} else if (i3 % 8 == 4 && swizzleState)
							{
								i2 -= 4;
								swizzleState = false;
							}

							/*byte currPix = (byte)fs.ReadByte();
							bitmap.SetPixel(i3, i2, Color.FromArgb(255, currPix & 0xF0, currPix & 0xF0, currPix & 0xF0));
							bitmap.SetPixel(i3 + 1, i2, Color.FromArgb(255, (currPix & 0x0F) << 4, (currPix & 0x0F) << 4, (currPix & 0x0F) << 4));*/
						}
					}
				}
				// 4 bit palette
				if (format == 0x8)
				{
					// read palette first
					fs.Seek(paletteOffset, SeekOrigin.Begin);
					Color[] palette = new Color[16];
					for (int i2 = 0; i2 < 16; ++i2)
					{
						// 16 bit colors with 1 bit alpha
						ushort col0 = (ushort)ReadBE16(fs);
						Color c0 = ReadColor(col0, colorFormat);
						palette[i2] = c0;
					}
					// okay, just read image now
					fs.Seek(dataOffset, SeekOrigin.Begin);
					int blockCount = 0;
					bool dont = false;
					int newWidth = (int)(Math.Ceiling(width / 8.0) * 8);
					for (int i2 = 0; i2 < height; i2 += 8)
					{
						for (int i3 = 0; i3 < newWidth || blockCount != 7; i3 += 2)
						{
							// dumb, again
							if (i3 % 8 == 0 && blockCount < 7 && i3 != 0 && !dont)
							{
								i3 -= 8;
								++blockCount;
								++i2;
								dont = true;
							}
							else if (i3 % 8 == 0 && blockCount == 7 && !dont)
							{
								blockCount = 0;
								i2 -= 7;
							}
							else
							{
								dont = false;
							}
							byte currPixels = (byte)fs.ReadByte();
							if (i3 < width && i2 < height)
							{
								bitmap.SetPixel(i3, i2, palette[(currPixels & 0xF0) >> 4]);
								bitmap.SetPixel(i3 + 1, i2, palette[currPixels & 0xF]);
							}
						}
						if (blockCount == 7)
						{
							i2 -= 7;
							blockCount = 0;
						}
					}
				}

				if (format == 0x9)
				{
					// 8 bit palette
					fs.Seek(paletteOffset, SeekOrigin.Begin);
					Color[] palette = new Color[256];
					for (int i2 = 0; i2 < 256; ++i2)
					{
						// 16 bit colors with 1 bit alpha
						ushort col0 = (ushort)ReadBE16(fs);
						Color c0 = ReadColor(col0, colorFormat);
						palette[i2] = c0;
					}
					// okay, just read image now
					fs.Seek(dataOffset, SeekOrigin.Begin);
					int blockCount = 0;
					bool dont = false;
					// round up width and height...
					int newWidth = (int)(Math.Ceiling(width / 8.0) * 8);
					for (int i2 = 0; i2 < height; i2 += 4)
					{
						for (int i3 = 0; i3 < newWidth || blockCount != 3; ++i3)
						{
							// dumb, again
							if (i3 % 8 == 0 && blockCount < 3 && i3 != 0 && !dont)
							{
								i3 -= 8;
								++blockCount;
								++i2;
								dont = true;
							}
							else if (i3 % 8 == 0 && blockCount == 3 && !dont)
							{
								blockCount = 0;
								i2 -= 3;
							}
							else
							{
								dont = false;
							}
							byte currPixels = (byte)fs.ReadByte();
							if (i3 < width && i2 < height)
							{
								bitmap.SetPixel(i3, i2, palette[currPixels]);
							}
						}
						if (blockCount == 3)
						{
							i2 -= 3;
							blockCount = 0;
						}
					}
				}

				if (format == 0x9 || format == 0x8 || format == 0xE)
				{
					bitmap.Save(path + i.ToString() + "." + format.ToString() + ".png", System.Drawing.Imaging.ImageFormat.Png);
				}

				fs.Seek(nextFile, SeekOrigin.Begin);
			}
		}

		struct ConvertingImage
		{
			public Bitmap image;
			public int format;
		}

		private struct PaletteImage
		{
			public List<int> pixelIndexes;
			public List<Color> palette;
			public byte[] convertedIndexes;
			public int width;
			public int height;
		}
		private static PaletteImage PaletteifyImage(Bitmap img, int size)
		{
			List<Color> usedColors = new List<Color>();
			List<int> pixelIndexes = new List<int>();
			int imgSize = img.Width * img.Height;
			for (int i = 0; i < img.Height; ++i)
			{
				for (int i2 = 0; i2 < img.Width; ++i2)
				{
					Color col = img.GetPixel(i2, i);
					// compare it against all previously established colors
					bool sameColor = false;
					float alpha = (float)Math.Round((col.A / 255.0f) * 8.0f);
					col = Color.FromArgb((byte)alpha, (col.R >> 3) << 3, (col.G >> 3) << 3, (col.B >> 3) << 3);
					for (int i3 = 0; i3 < usedColors.Count; ++i3)
					{
						if (usedColors[i3] == col)
						{
							sameColor = true;
							pixelIndexes.Add(i3);
							i3 = usedColors.Count;
						}
					}
					if (!sameColor)
					{
						usedColors.Add(col);
						pixelIndexes.Add(usedColors.Count - 1);
					}
				}
			}

			// okay great, now get the most similar ones until we're within space
			while (usedColors.Count > size)
			{
				int lowestDelta = 1024;
				int closest1 = 0;
				int closest2 = 0;
				for (int i = 0; i < usedColors.Count; ++i)
				{
					for (int i2 = i + 1; i2 < usedColors.Count; ++i2)
					{
						// let's actually delta in converted to A3RGB5
						int deltaColor = Math.Abs((usedColors[i].R >> 3) - (usedColors[i2].R >> 3));
						deltaColor += Math.Abs((usedColors[i].G >> 3) - (usedColors[i2].G >> 3));
						deltaColor += Math.Abs((usedColors[i].B >> 3) - (usedColors[i2].B >> 3));
						// make alpha really heavy for determining
						deltaColor += Math.Abs((usedColors[i].A) - (usedColors[i].A));
						if (deltaColor <= lowestDelta)
						{
							lowestDelta = deltaColor;
							closest1 = i;
							closest2 = i2;
						}
					}
				}
				// okay, now average out and then pop the closest colors
				usedColors[closest1] = Color.FromArgb(
					((int)usedColors[closest1].A + (int)usedColors[closest2].A) / 2,
					((int)usedColors[closest1].R + (int)usedColors[closest2].R) / 2,
					((int)usedColors[closest1].G + (int)usedColors[closest2].G) / 2,
					((int)usedColors[closest1].B + (int)usedColors[closest2].B) / 2);
				// adjust pixel indexes
				for (int i = 0; i < pixelIndexes.Count; ++i)
				{
					pixelIndexes[i] = ((pixelIndexes[i] == closest2) ? closest1 : pixelIndexes[i]);
					if (pixelIndexes[i] > closest2)
					{
						--pixelIndexes[i];
					}
				}
				usedColors.RemoveAt(closest2);
			}

			// ensure it meets a minimum size
			/*while (usedColors.Count < size)
			{
				usedColors.Add(new Color());
			}*/

			PaletteImage PI = new PaletteImage();
			PI.pixelIndexes = pixelIndexes;
			PI.palette = usedColors;
			PI.width = img.Width;
			PI.height = img.Height;
			return PI;
		}

		static int GetFormat(string fileName)
		{
			// retrieve the number between the . and extension
			for (int i = 0; i < fileName.Length; ++i)
			{
				if (fileName[i] == "."[0])
				{
					++i;
					string format = "";
					while (fileName[i] != "."[0])
					{
						if (i + 1 > fileName.Length)
						{
							// default value
							return 9;
						}
						format += fileName[i];
						++i;
					}
					try
					{
						return Int32.Parse(format);
					} catch
					{
						return 9;
					}
				}
			}
			// default value: 8 bit palette
			return 9;
		}

		static byte[] Blockify4(PaletteImage p)
		{
			int newWidth = (int)Math.Ceiling(p.width / 8.0f) * 8;
			int newHeight = (int)Math.Ceiling(p.height / 8.0f) * 8;
			byte[] newData = new byte[newWidth * newHeight / 2];
			int yLineSize = newWidth * 8;
			for (int i = 0; i < newHeight; ++i)
			{
				int yOutput = i / 8;
				for (int i2 = 0; i2 < newWidth; ++i2)
				{
					int xOutput = i2 / 8;
					int outputPos = ((xOutput * 0x40) + (i2 % 8) + (yOutput * yLineSize) + ((i % 8) * 0x8)) / 2;
					if (i2 < p.width && i < p.height)
					{
						if ((i2 % 2) == 1)
						{
							newData[outputPos] = (byte)(newData[outputPos] | p.pixelIndexes[i2 + (i * p.width)]);
						} else
						{
							newData[outputPos] = (byte)(p.pixelIndexes[i2 + (i * p.width)] << 4);
						}
					} else
					{
						// nothing...
					}
				}
			}
			return newData;
		}

		static byte[] Blockify8(PaletteImage p)
		{
			int newWidth = (int)Math.Ceiling(p.width / 8.0f) * 8;
			int newHeight = (int)Math.Ceiling(p.height / 8.0f) * 8;
			byte[] newData = new byte[newWidth * newHeight];
			int yLineSize = newWidth * 4;
			for (int i = 0; i < newHeight; ++i)
			{
				int yOutput = i / 4;
				for (int i2 = 0; i2 < newWidth; ++i2)
				{
					int xOutput = i2 / 8;
					int outputPos = (xOutput * 0x20) + (i2 % 8) + (yOutput * yLineSize) + ((i % 4) * 0x8);
					if (i2 < p.width && i < p.height)
					{
						newData[outputPos] = (byte)p.pixelIndexes[i2 + (i * p.width)];
					}
					else
					{

					}
				}
			}
			return newData;
		}

		static void WriteLE16(FileStream fs, ushort value)
		{
			byte[] tmp = BitConverter.GetBytes(value);
			byte tmp0 = tmp[0];
			tmp[0] = tmp[1];
			tmp[1] = tmp0;
			fs.Write(tmp);
		}

		static void WriteLE32(FileStream fs, int value)
		{
			byte[] tmp = BitConverter.GetBytes(value);
			byte tmp0 = tmp[0];
			byte tmp1 = tmp[1];
			tmp[0] = tmp[3];
			tmp[1] = tmp[2];
			tmp[2] = tmp1;
			tmp[3] = tmp0;
			fs.Write(tmp);
		}

		static void WriteLEU32(FileStream fs, uint value)
		{
			byte[] tmp = BitConverter.GetBytes(value);
			byte tmp0 = tmp[0];
			byte tmp1 = tmp[1];
			tmp[0] = tmp[3];
			tmp[1] = tmp[2];
			tmp[2] = tmp1;
			tmp[3] = tmp0;
			fs.Write(tmp);
		}

		static void WriteLEF32(FileStream fs, float value)
		{
			byte[] tmp = BitConverter.GetBytes(value);
			byte tmp0 = tmp[0];
			byte tmp1 = tmp[1];
			tmp[0] = tmp[3];
			tmp[1] = tmp[2];
			tmp[2] = tmp1;
			tmp[3] = tmp0;
			fs.Write(tmp);
		}

		static ushort ColorToBakuColor(Color col, int type = 0)
		{
			switch (type)
			{
				case 0x01000100:
					{
						byte r = (byte)(col.R >> 3);
						byte g = (byte)(col.G >> 2);
						byte b = (byte)(col.B >> 3);
						return (ushort)((r << 11) | (g << 5) | b);
					}
				default:
					{
						//float truAlpha = (float)Math.Round((col.A / 255.0f) * 8.0f);
						byte alpha = col.A;
						if (alpha == 0x8)
						{
							//a1rgb5
							byte r = (byte)(col.R >> 3);
							byte g = (byte)(col.G >> 3);
							byte b = (byte)(col.B >> 3);
							return (ushort)(0x8000 | (r << 10) | (g << 5) | b);
						}
						else
						{
							// a3rgb4
							byte r = (byte)(col.R >> 4);
							byte g = (byte)(col.G >> 4);
							byte b = (byte)(col.B >> 4);
							return (ushort)(((alpha) << 12) | (r << 8) | (g << 4) | b);
						}
					}
			}
		}

		static void ConvertToImages(string outputFile, string inputFolder)
		{
			// get all files in folder
			string[] files = Directory.GetFiles(inputFolder);
			ConvertingImage[] cImages = new ConvertingImage[files.Length];
			for (int i = 0; i < files.Length; ++i)
			{
				try
				{
					cImages[i].image = (Bitmap)Bitmap.FromFile(files[i]);
				} catch
				{
					Console.WriteLine("File found that isn't an image!");
					return;
				}
				cImages[i].format = GetFormat(files[i]);
			}

			PaletteImage[] pImages = new PaletteImage[files.Length];

			// largely re-using code from the GVR converter for palette generation
			for (int i = 0; i < cImages.Length; ++i)
			{
				switch (cImages[i].format)
				{
					case 8:
						// 16 bit image
							pImages[i] = PaletteifyImage(cImages[i].image, 16);
						// convert the data to 4 bit 8x8 blocks!
						pImages[i].convertedIndexes = Blockify4(pImages[i]);
						break;
					default:
						Console.WriteLine("Unsupported format " + cImages[i].format.ToString() + " for file " + files[i] + ", defaulting to 9");
						cImages[i].format = 9;
						goto case 9;
					case 9:
						pImages[i] = PaletteifyImage(cImages[i].image, 256);
						pImages[i].convertedIndexes = Blockify8(pImages[i]);
						break;
				}
			}
			// write the image file...
			int paletteStart = 0x20 * pImages.Length;
			int imageStart = paletteStart;
			// get starting place for the image data itself; done after palettes to keep consistent with official files
			for (int i = 0; i < pImages.Length; ++i)
			{
				if (cImages[i].format == 8)
				{
					imageStart += 0x20;
				} else if (cImages[i].format == 9)
				{
					imageStart += 0x200;
				}
			}

			FileStream fs;

			try
			{
				fs = File.Open(outputFile, FileMode.Create);
			} catch
			{
				Console.WriteLine("Failed to open file " + outputFile + " for writing!");
				return;
			}
			// start by writing file count
			WriteLE16(fs, (ushort)pImages.Length);
			WriteLE16(fs, 0);
			// write all the file offsets now
			for (int i = 0; i < pImages.Length; ++i)
			{
				// unused
				if (i != 0)
				{
					WriteLE32(fs, 0);
				}
				// file offset
				WriteLE32(fs, imageStart);
				imageStart += pImages[i].convertedIndexes.Length;
				// palette offset
				switch (cImages[i].format)
				{
					// palette, or no?
					case 8:
						WriteLE32(fs, paletteStart);
						paletteStart += 0x20;
						break;
					case 9:
						WriteLE32(fs, paletteStart);
						paletteStart += 0x200;
						break;
					default:
						WriteLE32(fs, 0);
						break;
				}
				// height and width
				WriteLE16(fs, (ushort)pImages[i].height);
				WriteLE16(fs, (ushort)pImages[i].width);
				// ???
				WriteLE32(fs, 0x00000101);
				// unused?
				WriteLE32(fs, 0);
				// format
				WriteLE32(fs, cImages[i].format);
				// palette format?
				switch (cImages[i].format) {
					case 8:
					case 9:
						WriteLE32(fs, 0x01000200);
						break;
					default:
						WriteLE32(fs, 0);
						break;
				}
			}
			// write palettes
			for (int i = 0; i < cImages.Length; ++i)
			{
				int i2 = 0;
				switch (cImages[i].format)
				{
					case 8:
						while (i2 < pImages[i].palette.Count && i2 < 16)
						{
							WriteLE16(fs, ColorToBakuColor(pImages[i].palette[i2]));
							++i2;
						}
						// flesh it out still
						while (i2 < 16)
						{
							WriteLE16(fs, 0);
							++i2;
						}
						break;
					case 9:
						while (i2 < pImages[i].palette.Count)
						{
							WriteLE16(fs, ColorToBakuColor(pImages[i].palette[i2]));
							++i2;
						}
						while (i2 < 256)
						{
							WriteLE16(fs, 0);
							++i2;
						}
						break;
				}
			}
			// write the image data themselves now
			for (int i = 0; i < cImages.Length; ++i)
			{
				fs.Write(pImages[i].convertedIndexes);
			}
			// that should be it!
			fs.Close();
		}

		static void ConvertToPalette(string input, string output)
		{
			FileStream fs;
			try
			{
				fs = File.Open(input, FileMode.Open, FileAccess.Read, FileShare.Read);
			}
			catch
			{
				Console.WriteLine("Failed to open file: " + input);
				return;
			}
			// start with how many images there are
			int imageCount = ReadBE16(fs);
			fs.Seek(-0x2, SeekOrigin.Current);
			// iterate over all of them
			for (int i = 0; i < imageCount; ++i)
			{
				// blank
				ReadBE32(fs);
				int dataOffset = ReadBE32(fs);
				int paletteOffset = ReadBE32(fs);
				int height = ReadBE16(fs);
				int width = ReadBE16(fs);
				// unknown
				int unknownBitflag = ReadBE32(fs);
				// unused?
				ReadBE32(fs);
				// format, presumably
				int format = ReadBE32(fs);
				// palette format?
				int colorFormat = ReadBE32(fs);
				long nextFile = fs.Position;

				switch (format)
				{
					case 0x8:
						width = 16;
						break;
					case 0x9:
						width = 256;
						break;
						// not palette based image, skip...
					default:
						continue;
				}

				Bitmap bitmap = new Bitmap(width, 1);
				// we just want to extract the palette onto the image...
				fs.Seek(paletteOffset, SeekOrigin.Begin);
				for (int i2 = 0; i2 < width; ++i2)
				{
					// just write the bitmap
					ushort c = (ushort)ReadBE16(fs);
					bitmap.SetPixel(i2, 0, ReadColor(c, colorFormat));
				}

				bitmap.Save(output + i.ToString() + ".png", System.Drawing.Imaging.ImageFormat.Png);

				// return to next file
				fs.Seek(nextFile, SeekOrigin.Begin);
			}
		}

		static void ConvertFromPalette(string inputFile, string inputFolder, string outputFile, string outputFolder)
		{
			FileStream fs;
			try
			{
				fs = File.Open(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);
			}
			catch
			{
				Console.WriteLine("Failed to open file: " + inputFile);
				return;
			}
			// just read the whole file
			fs.Seek(0, SeekOrigin.End);
			byte[] b = new byte[fs.Position];
			fs.Seek(0, SeekOrigin.Begin);
			fs.Read(b, 0, b.Length);
			fs.Close();

			// get all files in folder
			string[] files = Directory.GetFiles(inputFolder);
			ConvertingImage[] cImages = new ConvertingImage[files.Length];
			for (int i = 0; i < files.Length; ++i)
			{
				try
				{
					cImages[i].image = (Bitmap)Bitmap.FromFile(files[i]);
				}
				catch
				{
					Console.WriteLine("File found that isn't an image!");
					return;
				}
			}

			// get file count for boundaries...
			int fileCount = (b[1] << 8) | b[0];
			for (int i = 0; i < cImages.Length; ++i)
			{
				int fId;
				try
				{
					fId = Int32.Parse(Path.GetFileNameWithoutExtension(files[i]));
				} catch
				{
					Console.WriteLine("Palette image in incorrect filename format!");
					return;
				}
				int imgOffs = fId * 0x20;
				// ensure image matches file format & bounds
				int imageType = b[imgOffs + 0x1B];
				int paletteIterations = 0;
				switch (imageType)
				{
					case 0x8:
						if (cImages[i].image.Width < 16)
						{
							Console.WriteLine("Image " + i.ToString() + " is not correct width for format! Expected 16, got " + cImages[i].image.Width);
							return;
						}
						// image width fine, just set up iteration count now then
						paletteIterations = 16;
						break;
					case 0x9:
						if (cImages[i].image.Width < 256)
						{
							Console.WriteLine("Image " + i.ToString() + " is not correct width for format! Expected 256, got " + cImages[i].image.Width);
							return;
						}
						paletteIterations = 256;
						break;
					default:
						Console.WriteLine("Trying to replace palette of non-paletted or non-supported image format!");
						return;
				}
				int paletteFormat = (b[imgOffs + 0x1C] << 24) | (b[imgOffs + 0x1D] << 16) | (b[imgOffs + 0x1E] << 8) | b[imgOffs + 0x1F];
				int paletteOffset = (b[imgOffs + 0x8] << 24) | (b[imgOffs + 0x9] << 16) | (b[imgOffs + 0xA] << 8) | b[imgOffs + 0xB];
				for (int i2 = 0; i2 < paletteIterations; ++i2)
				{
					// convert the color to the proper format...!
					Color colo = cImages[i].image.GetPixel(i2, 0);
					colo = Color.FromArgb(colo.A == 255 ? 8 : colo.A >> 5, colo.R, colo.G, colo.B);
					ushort col = ColorToBakuColor(colo, paletteFormat);
					Color refTest = cImages[i].image.GetPixel(i2, 0);
					Color test = ReadColor(col, paletteFormat);
					b[paletteOffset + (i2 * 2)] = (byte)(col >> 8);
					b[paletteOffset + (i2 * 2) + 1] = (byte)(col & 0xFF);
				}
			}
			// win?
			fs = File.Open(outputFile, FileMode.Create, FileAccess.Write);
			fs.Write(b, 0, b.Length);
			fs.Close();
			// and our optional output path...
			if (outputFolder != "")
			{
				ConvertImages(outputFile, outputFolder);
			}
		}


		static void AddToArray(byte[] array, int toAdd, int offs)
		{
			// endianness...
			byte[] tmp = BitConverter.GetBytes(toAdd);
			byte[] tmp2 = BitConverter.GetBytes(toAdd);
			tmp[0] = tmp2[3];
			tmp[1] = tmp2[2];
			tmp[2] = tmp2[1];
			tmp[3] = tmp2[0];
			Array.Copy(tmp, 0, array, offs, 4);
		}

		static void AddToArray16(byte[] array, ushort toAdd, int offs)
		{
			// endianness...
			byte[] tmp = BitConverter.GetBytes(toAdd);
			byte[] tmp2 = BitConverter.GetBytes(toAdd);
			tmp[0] = tmp2[1];
			tmp[1] = tmp2[0];
			Array.Copy(tmp, 0, array, offs, 2);
		}


		struct BakuImgHeader
		{
			public int dataOffset;
			public int paletteOffset;
			public short height;
			public short width;
			public int flags;
			public int padding;
			public int format;
			public int colorFormat;
		}

		class DictHelper
		{
			public byte[] data;
			public int offset;
		}

		struct BakuUV
		{
			public ushort image;
			public float x1;
			public float y1;
			public float x2;
			public float y2;
		}

		static void AppendToFile(string inputFile, string inputFolder, string output)
		{
			FileStream fs;
			try
			{
				fs = File.Open(inputFile, FileMode.Open, FileAccess.Read);
			} catch
			{
				Console.WriteLine("Failed to open file: " + inputFile);
				return;
			}
			// initial header...
			int imagesStart = ReadBE32(fs);
			int imageInfoStart = ReadBE32(fs);
			// start by parsing the images and storing them to re-write later
			fs.Seek(imagesStart, SeekOrigin.Begin);
			int imageCount = ReadBE16(fs);
			BakuImgHeader[] oldImages = new BakuImgHeader[imageCount];
			fs.Seek(-2, SeekOrigin.Current);
			for (int i = 0; i < imageCount; ++i)
			{
				// 0 padding
				ReadBE32(fs);
				oldImages[i].dataOffset = ReadBE32(fs);
				oldImages[i].paletteOffset = ReadBE32(fs);
				oldImages[i].height = (short)ReadBE16(fs);
				oldImages[i].width = (short)ReadBE16(fs);
				oldImages[i].flags = ReadBE32(fs);
				// should be unused always but just in case
				oldImages[i].padding = ReadBE32(fs);
				oldImages[i].format = ReadBE32(fs);
				oldImages[i].colorFormat = ReadBE32(fs);
			}
			// read all the palettes and texture data
			Dictionary<int, DictHelper> oldPalettes = new Dictionary<int, DictHelper>();
			Dictionary<int, DictHelper> oldTextures = new Dictionary<int, DictHelper>();
			for (int i = 0; i < imageCount; ++i)
			{
				if (!oldPalettes.ContainsKey(oldImages[i].paletteOffset))
				{
					byte[] pal = null;
					if (oldImages[i].format == 0x8)
					{
						pal = new byte[16*2];
					} else if (oldImages[i].format == 0x9)
					{
						pal = new byte[256 * 2];
					}
					// account for non-paletted textures, should they exist (they shouldn't, but y'know.)
					if (pal != null)
					{
						fs.Seek(oldImages[i].paletteOffset + imagesStart, SeekOrigin.Begin);
						fs.Read(pal, 0, pal.Length);
						DictHelper dh = new DictHelper();
						dh.data = pal;
						oldPalettes.Add(oldImages[i].paletteOffset, dh);
					}
				}
				if (!oldTextures.ContainsKey(oldImages[i].dataOffset))
				{
					byte[] tex = null;
					if (oldImages[i].format == 0x8 || oldImages[i].format == 14)
					{
						tex = new byte[oldImages[i].height * oldImages[i].width / 2];
					}
					else if (oldImages[i].format == 0x9)
					{
						tex = new byte[oldImages[i].height * oldImages[i].width];
					}
					fs.Seek(oldImages[i].dataOffset + imagesStart, SeekOrigin.Begin);
					fs.Read(tex, 0, tex.Length);
					DictHelper dh = new DictHelper();
					dh.data = tex;
					oldTextures.Add(oldImages[i].dataOffset, dh);
				}
			}

			// load the other images
			string[] files = Directory.GetFiles(inputFolder);

			// get formats, yadda yadda...
			ConvertingImage[] cImages = new ConvertingImage[files.Length];
			for (int i = 0; i < files.Length; ++i)
			{
				try
				{
					cImages[i].image = (Bitmap)Bitmap.FromFile(files[i]);
				}
				catch
				{
					Console.WriteLine("File found that isn't an image!");
					return;
				}
				cImages[i].format = GetFormat(files[i]);
			}

			PaletteImage[] pImages = new PaletteImage[files.Length];
			BakuImgHeader[] newImages = new BakuImgHeader[files.Length];

			// largely re-using code from the GVR converter for palette generation
			for (int i = 0; i < cImages.Length; ++i)
			{
				BakuImgHeader hdr = new BakuImgHeader();
				switch (cImages[i].format)
				{
					case 8:
						// 16 bit image
						pImages[i] = PaletteifyImage(cImages[i].image, 16);
						// convert the data to 4 bit 8x8 blocks!
						pImages[i].convertedIndexes = Blockify4(pImages[i]);
						break;
					default:
						Console.WriteLine("Unsupported format " + cImages[i].format.ToString() + " for file " + files[i] + ", defaulting to 9");
						cImages[i].format = 9;
						goto case 9;
					case 9:
						pImages[i] = PaletteifyImage(cImages[i].image, 256);
						pImages[i].convertedIndexes = Blockify8(pImages[i]);
						break;
				}
				hdr.format = cImages[i].format;
				hdr.colorFormat = 0x01000200;
				hdr.flags = 0x00000101;
				hdr.height = (short)pImages[i].height;
				hdr.width = (short)pImages[i].width;
				newImages[i] = hdr;
			}
			// get the image headerinfo
			fs.Seek(imageInfoStart, SeekOrigin.Begin);
			// yeah no idea what these are tbh
			int infoMagic1 = ReadBE32(fs);
			int infoMagic2 = ReadBE32(fs);
			int mainInfoPtr = ReadBE32(fs) + imageInfoStart;
			int UVPtr = ReadBE32(fs) + imageInfoStart;
			int unkPtr = ReadBE32(fs) + imageInfoStart;

			// let's start with the main info stuff
			fs.Seek(mainInfoPtr, SeekOrigin.Begin);
			int mainInfoCount = ReadBE32(fs);
			int mainInfoSize = ReadBE32(fs) - ((mainInfoCount * 4) + 8);
			// making this a list so we can just append to it later!
			List<int> mainInfoPtrs = new List<int>();
			for (int i = 0; i < mainInfoCount; ++i)
			{
				mainInfoPtrs.Add(ReadBE32(fs));
			}
			// now we just read the base data to rewrite
			byte[] baseMainInfoData = new byte[mainInfoSize];
			fs.Read(baseMainInfoData, 0, mainInfoSize);

			// and now read the UV maps
			fs.Seek(UVPtr, SeekOrigin.Begin);
			int UVCount = ReadBE32(fs);
			// again, make a list so we can append it later
			List<BakuUV> UVMaps = new List<BakuUV>();
			// UV section file size--not needed
			ReadBE32(fs);
			for (int i = 0; i < UVCount; ++i)
			{
				BakuUV currUV = new BakuUV();
				currUV.image = (ushort)ReadBE16(fs);
				// padding
				ReadBE16(fs);
				currUV.x1 = ReadBEF32(fs);
				currUV.y1 = ReadBEF32(fs);
				currUV.x2 = ReadBEF32(fs);
				currUV.y2 = ReadBEF32(fs);
				UVMaps.Add(currUV);
			}

			// done with the file
			fs.Close();

			// let's reconstruct the image file now
			int totalFileSize = 0;
			totalFileSize = (imageCount + files.Length) * 0x20;
			int currData = totalFileSize;
			for (int i = 0; i < oldPalettes.Count; ++i)
			{
				totalFileSize += oldPalettes.ElementAt(i).Value.data.Length;
				totalFileSize += 0x20 - (oldPalettes.ElementAt(i).Value.data.Length % 0x20);
			}
			for (int i = 0; i < oldTextures.Count; ++i)
			{
				totalFileSize += oldTextures.ElementAt(i).Value.data.Length;
				totalFileSize += 0x20 - (oldTextures.ElementAt(i).Value.data.Length % 0x20);
			}
			// new images
			for (int i = 0; i < files.Length; ++i)
			{
				totalFileSize += pImages[i].palette.Count * 2;
				totalFileSize += pImages[i].convertedIndexes.Length;
				// align these to 0x20
				totalFileSize += 0x20 - ((pImages[i].palette.Count * 2) % 0x20);
				totalFileSize += 0x20 - ((pImages[i].convertedIndexes.Length) % 0x20);
			}
			// create buffer now
			byte[] buff = new byte[totalFileSize];
			for (int i = 0; i < oldPalettes.Count; ++i)
			{
				DictHelper dh = oldPalettes.ElementAt(i).Value;
				Array.Copy(dh.data, 0, buff, currData, dh.data.Length);
				dh.offset = currData;
				currData += dh.data.Length;
				currData += 0x20 - (oldPalettes.ElementAt(i).Value.data.Length % 0x20);
			}
			for (int i = 0; i < oldTextures.Count; ++i)
			{
				DictHelper dh = oldTextures.ElementAt(i).Value;
				Array.Copy(dh.data, 0, buff, currData, dh.data.Length);
				dh.offset = currData;
				currData += dh.data.Length;
				currData += 0x20 - (oldTextures.ElementAt(i).Value.data.Length % 0x20);
			}
			int[] newPalOffs = new int[newImages.Count()];
			int[] newTexOffs = new int[newImages.Count()];
			// add new files now too
			for (int i = 0; i < newImages.Count(); ++i)
			{
				newPalOffs[i] = currData;
				for (int i2 = 0; i2 < pImages[i].palette.Count(); ++i2)
				{
					AddToArray16(buff, ColorToBakuColor(pImages[i].palette[i2]), currData);
					currData += 2;
				}
				currData += 0x20 - (pImages[i].palette.Count() * 2 % 0x20);
			}
			for (int i = 0; i < newImages.Count(); ++i)
			{
				newTexOffs[i] = currData;
				Array.Copy(pImages[i].convertedIndexes, 0, buff, currData, pImages[i].convertedIndexes.Count());
				currData += pImages[i].convertedIndexes.Count();
				currData += 0x20 - (pImages[i].convertedIndexes.Length % 0x20);
			}
			// write the headers again now
			for (int i = 0; i < oldImages.Count(); ++i)
			{
				// get the new offsets
				int dataOffs = oldTextures[oldImages[i].dataOffset].offset;
				AddToArray(buff, dataOffs, (0x20 * i) + 4);
				if (oldImages[i].paletteOffset != 0)
				{
					int palOffs = oldPalettes[oldImages[i].paletteOffset].offset;
					AddToArray(buff, palOffs, (0x20 * i) + 8);
				}
				// and just copy the rest raw
				AddToArray16(buff, (ushort)oldImages[i].height, (0x20 * i) + 0xC);
				AddToArray16(buff, (ushort)oldImages[i].width, (0x20 * i) + 0xE);
				AddToArray(buff, oldImages[i].flags, (0x20 * i) + 0x10);
				AddToArray(buff, oldImages[i].padding, (0x20 * i) + 0x14);
				AddToArray(buff, oldImages[i].format, (0x20 * i) + 0x18);
				AddToArray(buff, oldImages[i].colorFormat, (0x20 * i) + 0x1C);
			}
			// and new headers
			for (int i = 0; i < newImages.Count(); ++i)
			{
				int newBase = (oldImages.Count() + i) * 0x20;
				AddToArray(buff, newTexOffs[i], newBase + 4);
				AddToArray(buff, newPalOffs[i], newBase + 8);
				AddToArray16(buff, (ushort)newImages[i].height, newBase + 0xC);
				AddToArray16(buff, (ushort)newImages[i].width, newBase + 0xE);
				AddToArray(buff, newImages[i].flags, newBase + 0x10);
				AddToArray(buff, 0, newBase + 0x14);
				AddToArray(buff, newImages[i].format, newBase + 0x18);
				AddToArray(buff, newImages[i].colorFormat, newBase + 0x1C);
			}
			// write image count
			AddToArray16(buff, (ushort)(oldImages.Count() + newImages.Count()), 0);
			// and write output
			try
			{
				fs = File.Open(output, FileMode.Create, FileAccess.Write);
			}
			catch
			{
				Console.WriteLine("Failed to open file: " + output);
			}
			// write initial header
			WriteLE32(fs, 0x20);
			// pad out a little...
			int padAmount = 0x20 - (totalFileSize % 0x20);
			totalFileSize += padAmount;
			WriteLE32(fs, totalFileSize + 0x20);
			// and 0x18 padding
			for (int i = 0; i < 6; ++i)
			{
				WriteLE32(fs, 0);
			}
			// and just write the whole damn thing
			fs.Write(buff, 0, buff.Length);

			// pad out to match our new start
			for (int i = 0; i < padAmount; ++i)
			{
				fs.WriteByte(0);
			}

			int mainInfoPtrsBaseSize = mainInfoPtrs.Count;

			// add new files to the list
			for (int i = 0; i < newImages.Length; ++i)
			{
				// 0x5C is the size of data we add per file, and subtract 1 for max iterations since that gets re-added later
				mainInfoPtrs.Add(mainInfoSize + 8 + (mainInfoPtrsBaseSize * 4) + (i * 0x5C));
				// add UV maps too
				BakuUV newUV = new BakuUV();
				newUV.image = (ushort)(oldImages.Length + i);
				newUV.x1 = 0f;
				newUV.y1 = 0f;
				newUV.x2 = 1f;
				newUV.y2 = 1f;
				UVMaps.Add(newUV);
			}

			// adjust pointers
			for (int i = 0; i < mainInfoPtrs.Count; ++i)
			{
				mainInfoPtrs[i] += newImages.Length * 4;
			}
			// adjust mainInfoSize to simplify things
			mainInfoSize += newImages.Length * 0x5C;

			// now, write the file info
			WriteLE32(fs, infoMagic1);
			WriteLE32(fs, infoMagic2);
			// offset to main info
			WriteLE32(fs, 0x14);
			// offset to UV Maps
			// TODO: on everything that references mainInfoSize, to update it to incorporate our new sizes
			WriteLE32(fs, mainInfoSize + 8 + (mainInfoPtrs.Count() * 4) + 0x14);
			// offset to ? data
			WriteLE32(fs, mainInfoSize + 8 + (mainInfoPtrs.Count() * 4) + 8 + (UVMaps.Count() * 0x14) + 0x14);
			// now just straight up write the main info
			// count
			WriteLE32(fs, mainInfoPtrs.Count());
			// size of section (i'm not entirely sure this is even necessary but i want to include it anyways)
			WriteLE32(fs, mainInfoSize + 8 + (mainInfoPtrs.Count() * 4));
			// ptrs
			for (int i = 0; i < mainInfoPtrs.Count(); ++i)
			{
				WriteLE32(fs, mainInfoPtrs[i]);
			}
			// now main info
			// TODO: add our additional data here
			fs.Write(baseMainInfoData, 0, baseMainInfoData.Length);
			// write data for our new images
			for (int i = 0; i < newImages.Length; ++i)
			{
				// ? 0
				WriteLE32(fs, 0);
				// image count
				WriteLE32(fs, 1);
				// total size(?)
				WriteLE32(fs, 0x5C);
				// first image offset; we'll have written 0x10 bytes after this, so
				WriteLE32(fs, 0x10);
				// count, once more, plus struct size?
				WriteLE32(fs, 0x00010048);
				// struct size
				WriteLE16(fs, 0x12);
				// type...?
				WriteLE16(fs, 0);
				// ??
				WriteLE16(fs, 0x0400);
				// what UVMap to render
				WriteLE16(fs, (ushort)(UVCount + i));
				// don't know what any of the rest of this junk is
				for (int i2 = 0; i2 < 6; ++i2)
				{
					WriteLE32(fs, 0);
				}
				WriteLE32(fs, 0x00000200);
				for (int i2 = 0; i2 < 4; ++i2)
				{
					WriteLEU32(fs, 0xFF99FFE0);
				}
				// image center...?
				WriteLE16(fs, 0x0400);
				WriteLE16(fs, 0x0400);
				for (int i2 = 0; i2 < 4; ++i2)
				{
					WriteLEU32(fs, 0xFFFFFFFF);
				}
			}
			// UV maps now
			WriteLE32(fs, UVMaps.Count());
			// size
			WriteLE32(fs, 8 + (UVMaps.Count * 0x14));
			// iterate over and write all the UV maps
			for (int i = 0; i < UVMaps.Count; ++i)
			{
				WriteLE16(fs, UVMaps[i].image);
				WriteLE16(fs, 0);
				WriteLEF32(fs, UVMaps[i].x1);
				WriteLEF32(fs, UVMaps[i].y1);
				WriteLEF32(fs, UVMaps[i].x2);
				WriteLEF32(fs, UVMaps[i].y2);
			}

			// and now just write our mystery data
			WriteLE32(fs, 0);
			WriteLE32(fs, 0x8);
			WriteLE32(fs, 0);

			fs.Close();
		}

		static void Main(string[] args)
		{
			int mode = 0;
			if (args.Length < 3)
			{
				Console.WriteLine("BakuImage.exe [mode] [input] [output/input 2] <output> <output 2>\r\nModes:\r\n-p: convert images to png\r\n-b: convert folder of images to bakugan format\r\n-ao: extract the palette from an image\r\n-ai: replace the palette in an image; folder holding palettes in input 1, original bakugan image file in input 2, output to output (can use output 2 to then automatically convert it to pngs to preview as well)\r\n-ap: appends images to a HUD archive, including setting them up to be rendered. Base file in input1, folder of images to append in input2, output file in output");
				return;
			}
			if (args[0] == "-p")
			{
				mode = 0;
			}
			else if (args[0] == "-b")
			{
				mode = 1;
			}
			else if (args[0] == "-ao")
			{
				mode = 2;
			}
			else if (args[0] == "-ai")
			{
				mode = 3;
			} else if (args[0] == "-ap")
			{
				mode = 4;
			}
			else
			{
				Console.WriteLine("BakuImage.exe [mode] [input] [output/input 2] <output> <output 2>\r\nModes:\r\n-p: convert images to png\r\n-b: convert folder of images to bakugan format\r\n-ao: extract the palette from an image\r\n-ai: replace the palette in an image; folder holding palettes in input 1, original bakugan image file in input 2, output to output (can use output 2 to then automatically convert it to pngs to preview as well)\r\n-ap: appends images to a HUD archive, including setting them up to be rendered. Base file in input1, folder of images to append in input2, output file in output");
				return;
			}
			switch (mode)
			{
				case 0:
					{
						string tmp = args[2];
						if (tmp[tmp.Length - 1] != "\\"[0])
						{
							tmp += "\\";
						}
						if (!Directory.Exists(tmp))
						{
							Directory.CreateDirectory(tmp);
						}
						ConvertImages(args[1], tmp);
					}
					break;
				case 1:
					{
						string tmp = args[1];
						if (tmp[tmp.Length - 1] != "\\"[0])
						{
							tmp += "\\";
						}
						if (!Directory.Exists(tmp))
						{
							Console.WriteLine("Can't find folder: " + tmp);
							return;
						}
						ConvertToImages(args[2], tmp);
					}
					break;
				case 2:
					{
						string tmp = args[2];
						if (tmp[tmp.Length - 1] != "\\"[0])
						{
							tmp += "\\";
						}
						if (!Directory.Exists(tmp))
						{
							Directory.CreateDirectory(tmp);
						}
						ConvertToPalette(args[1], tmp);
					}
					break;
				case 3:
					{
						if (args.Length < 4)
						{
							Console.WriteLine("Not enough arguments!");
							return;
						}
						string tmp = args[1];
						if (tmp[tmp.Length - 1] != "\\"[0])
						{
							tmp += "\\";
						}
						if (!Directory.Exists(tmp))
						{
							Console.WriteLine("Can't find folder: " + tmp);
							return;
						}
						string extraConvert = "";
						if (args.Length > 3)
						{
							extraConvert = args[4];
							if (extraConvert[extraConvert.Length - 1] != "\\"[0])
							{
								extraConvert += "\\";
							}
							if (!Directory.Exists(extraConvert))
							{
								Directory.CreateDirectory(extraConvert);
							}
						}
						ConvertFromPalette(args[2], tmp, args[3], extraConvert);
					}
					break;
				case 4:
					{
						if (args.Length < 4)
						{
							Console.WriteLine("Not enough arguments!");
							return;
						}
						string tmp = args[2];
						if (tmp[tmp.Length - 1] != "\\"[0])
						{
							tmp += "\\";
						}
						if (!Directory.Exists(tmp))
						{
							Console.WriteLine("Can't find folder: " + tmp);
							return;
						}
						AppendToFile(args[1], tmp, args[3]);
					}
					break;
			}
		}
	}
}
