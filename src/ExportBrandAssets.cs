using System;using System.Drawing;using System.Drawing.Imaging;using System.IO;using System.Collections.Generic;using Briefly;
// Render the existing website/native vector mark, without changing its geometry or colours.
class ExportBrandAssets {
 static byte[] Png(int size){using(var b=new Bitmap(size,size,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(b))using(var stream=new MemoryStream()){g.Clear(Color.Transparent);AnimatedLogo.Render(g,size,size,0,false);b.Save(stream,ImageFormat.Png);return stream.ToArray();}}
 static byte[] Dib(int size){using(var b=new Bitmap(size,size,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(b))using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){
  g.Clear(Color.Transparent);AnimatedLogo.Render(g,size,size,0,false);int maskStride=((size+31)/32)*4;
  w.Write(40);w.Write(size);w.Write(size*2);w.Write((ushort)1);w.Write((ushort)32);w.Write(0);w.Write(size*size*4+maskStride*size);w.Write(0);w.Write(0);w.Write(0);w.Write(0);
  for(int y=size-1;y>=0;y--)for(int x=0;x<size;x++){var c=b.GetPixel(x,y);w.Write(c.B);w.Write(c.G);w.Write(c.R);w.Write(c.A);}
  for(int y=size-1;y>=0;y--){byte[] mask=new byte[maskStride];for(int x=0;x<size;x++)if(b.GetPixel(x,y).A==0)mask[x/8]|=(byte)(128>>(x%8));w.Write(mask);}return stream.ToArray();
 }}
 static int Main(string[] args){string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);var sizes=new[]{16,24,32,48,64,128,256};var images=new List<byte[]>();foreach(int size in sizes)images.Add(Dib(size));
  using(var stream=File.Create(Path.Combine(root,"briefly.ico")))using(var w=new BinaryWriter(stream)){w.Write((ushort)0);w.Write((ushort)1);w.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){int size=sizes[i];w.Write((byte)(size==256?0:size));w.Write((byte)(size==256?0:size));w.Write((byte)0);w.Write((byte)0);w.Write((ushort)1);w.Write((ushort)32);w.Write(images[i].Length);w.Write(offset);offset+=images[i].Length;}foreach(var image in images)w.Write(image);}
  foreach(int size in new[]{16,32,64,180,512})File.WriteAllBytes(Path.Combine(root,"logo-"+size+".png"),Png(size));
  foreach(int size in sizes)using(var icon=new Icon(Path.Combine(root,"briefly.ico"),size,size))using(var bitmap=icon.ToBitmap()){if(bitmap.Width<16||bitmap.Height<16)throw new Exception("Icon frame cannot be rendered by Windows");Console.WriteLine("Icon requested "+size+", Windows selected "+icon.Width+", bitmap "+bitmap.Width);}
  Console.WriteLine("Existing Briefly vector brand rendered at 16/24/32/48/64/128/256 ICO and 180/512 PNG. Windows icon decode verified.");return 0;
 }
}
