using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Xml;

namespace _64Inject
{
    public class _64Injector
    {
        public const string Release = "1.8 debug"; //CllVersionReplace "major.minor stability"

        public string BasePath;
        public string ShortName;
        public string LongName;
        public bool DarkFilter;
        public bool Widescreen;
        public string Zoom;
        public string InPath;
        public string RomPath;
        public string IniPath;
        public string BootTvPath;
        public string BootDrcPath;
        public string IconPath;
        public string OutPath;
        public bool Encrypt;

        private VCN64 _base;
        public RomN64 Rom;
        public VCN64ConfigFile Ini;
        public float Scale;
        public BootImage BootTvImg;
        public BootImage BootDrcImg;
        public IconImage IconImg;

        public bool BaseIsLoaded
        {
            get { return _base != null; }
        }

        public bool RomIsLoaded
        {
            get { return Rom != null && Rom.IsValid; }
        }

        public bool IniIsLoaded
        {
            get { return Ini != null && Ini.IsValid; }
        }

        public string LoadedBase
        {
            get
            {
                if (_base != null)
                    return _base.ToString();
                else
                    return "";
            }
        }

        public string ShortNameASCII
        {
            get
            {
                char[] array = Useful.Windows1252ToASCII(ShortName, '_').ToCharArray();
                char[] invalid = Path.GetInvalidFileNameChars();

                for (int i = 0; i < array.Length; i++)
                {
                    foreach (char c in invalid)
                    {
                        if (array[i] == c)
                            array[i] = '_';
                    }
                }
                
                return new string(array);
            }
        }

        public string TitleId
        {
            get
            {
                if (BaseIsLoaded && RomIsLoaded)
                {
                    uint crc = Rom.HashCRC16;

                    if (IniIsLoaded)
                        crc += Ini.HashCRC16;
                    else
                        crc += Cll.Security.ComputeCRC16_ARC(new byte[] { }, 0, 0);
                    crc >>= 1;

                    int flags = _base.Index;
                    flags |= DarkFilter ? 0x80 : 0;
                    flags |= Widescreen ? 0x40 : 0;
                    flags |= Scale != 1.0F ? 0x20 : 0;

                    return "0005000064" + crc.ToString("X4") + ((byte)(flags)).ToString("X2");
                }
                else
                    return "";
            }
        }
        

        public _64Injector()
        {
            BasePath = null;
            ShortName = null;
            LongName = null;
            DarkFilter = true;
            Widescreen = false;
            Zoom = null;
            InPath = null;
            RomPath = null;
            IniPath = null;
            IconPath = null;
            BootTvPath = null;
            BootDrcPath = null;
            OutPath = null;
            Encrypt = true;

            _base = GetLoadedBase();
            Rom = null;
            Ini = null;
            Scale = 1.0F;
            BootTvImg = new BootImage();
            BootDrcImg = new BootImage();
            IconImg = new IconImage();
        }

        public bool Inject()
        {
            _base = GetLoadedBase();
            bool _continue = BaseIsLoaded;
            if (_continue)
            {
                Cll.Log.WriteLine("Base info:");
                Cll.Log.WriteLine(_base.ToString());
            }
            else
                Cll.Log.WriteLine("The base is not loaded.");

            if (_continue)
                _continue = InjectGameLayout();
                
            if (_continue)
                _continue = InjectImages();

            if (_continue)
                _continue = InjectMeta();

            if (_continue)
                _continue = InjectIni();

            if (_continue)
                _continue = InjectRom();

            if (_continue)
            {
                if (Encrypt)
                {
                    Cll.Log.WriteLine("Creating encrypted output.");
                    string inPath = Environment.CurrentDirectory + "/base";
                    _continue = NusContent.Encrypt(inPath, OutPath);
                }
                else
                {
                    Cll.Log.WriteLine("Creating unencrypted output.");
                    _continue = Useful.DirectoryCopy("base", OutPath, true);
                }          
            }

            if (_continue)
                Cll.Log.WriteLine("Injection completed successfully!");
            else
                Cll.Log.WriteLine("The injection failed.");

            return _continue;
        }
        
        private bool InjectGameLayout()
        {
            FileStream fs = null;
            try
            {
                Cll.Log.WriteLine("Editing \"FrameLayout.arc\" file.");

                byte darkFilterB = (byte)(DarkFilter ? 1 : 0);
                byte[] widescreenB = Widescreen ?
                    new byte[] { 0x44, 0xF0, 0, 0 } :
                    new byte[] { 0x44, 0xB4, 0, 0 };
                byte[] scaleB = BitConverter.GetBytes(Scale);

                byte[] magic = new byte[4];
                uint offset = 0;
                uint size = 0;
                byte[] offsetB = new byte[4];
                byte[] sizeB = new byte[4];
                byte[] nameB = new byte[0x18];

                fs = File.Open("base/content/FrameLayout.arc", FileMode.Open);
                fs.Read(magic, 0, 4);

                if (magic[0] == 'S' &&
                    magic[1] == 'A' &&
                    magic[2] == 'R' &&
                    magic[3] == 'C')
                {
                    fs.Position = 0x0C;
                    fs.Read(offsetB, 0, 4);
                    offset = (uint)(offsetB[0] << 24 |
                        offsetB[1] << 16 |
                        offsetB[2] << 8 |
                        offsetB[3]);
                    fs.Position = 0x38;
                    fs.Read(offsetB, 0, 4);
                    offset += (uint)(offsetB[0] << 24 |
                        offsetB[1] << 16 |
                        offsetB[2] << 8 |
                        offsetB[3]);

                    fs.Position = offset;
                    fs.Read(magic, 0, 4);

                    if (magic[0] == 'F' &&
                        magic[1] == 'L' &&
                        magic[2] == 'Y' &&
                        magic[3] == 'T')
                    {
                        fs.Position = offset + 0x04;
                        fs.Read(offsetB, 0, 4);
                        offsetB[0] = 0;
                        offsetB[1] = 0;
                        offset += (uint)(offsetB[0] << 24 |
                            offsetB[1] << 16 |
                            offsetB[2] << 8 |
                            offsetB[3]);
                        fs.Position = offset;

                        while (true)
                        {
                            fs.Read(magic, 0, 4);
                            fs.Read(sizeB, 0, 4);
                            size = (uint)(sizeB[0] << 24 |
                                sizeB[1] << 16 |
                                sizeB[2] << 8 |
                                sizeB[3]);                                

                            if (magic[0] == 'p' &&
                                magic[1] == 'i' &&
                                magic[2] == 'c' &&
                                magic[3] == '1')
                            {
                                fs.Position = offset + 0x0C;
                                fs.Read(nameB, 0, 0x18);
                                int count = Array.IndexOf(nameB, (byte)0);
                                string name = Encoding.ASCII.GetString(nameB, 0, count);

                                if (name == "frame")
                                {
                                    fs.Position = offset + 0x44;//Scale
                                    fs.WriteByte(scaleB[3]);
                                    fs.WriteByte(scaleB[2]);
                                    fs.WriteByte(scaleB[1]);
                                    fs.WriteByte(scaleB[0]);
                                    fs.Position = offset + 0x48;//Scale
                                    fs.WriteByte(scaleB[3]);
                                    fs.WriteByte(scaleB[2]);
                                    fs.WriteByte(scaleB[1]);
                                    fs.WriteByte(scaleB[0]);
                                    fs.Position = offset + 0x4C;//Widescreen
                                    fs.Write(widescreenB, 0, 4);
                                }
                                else if (name == "frame_mask")
                                {
                                    fs.Position = offset + 0x08;//Dark filter
                                    fs.WriteByte(darkFilterB);                                        
                                }
                                else if (name == "power_save_bg")
                                {
                                    Cll.Log.WriteLine("\"FrameLayout.arc\" file editing successfully.");
                                    return true;
                                }

                                offset += size;
                                fs.Position = offset;
                            }
                            else if (offset + size >= fs.Length)
                            {
                            }
                            else
                            {
                                offset += size;
                                fs.Position = offset;
                            }
                        }
                    }
                }                    
                fs.Close();
            }
            catch { Cll.Log.WriteLine("Error editing \"FrameLayout.arc\"."); }
            finally { if (fs != null) fs.Close(); }
            
            return false;
        }

        private bool InjectImages()
        {
            string currentDir = Environment.CurrentDirectory;
            Bitmap bootTvImg;
            Bitmap bootDrcImg;
            Bitmap iconImg;
            Bitmap tmp;
            Graphics g;

            try
            {
                Cll.Log.WriteLine("Creating bitmaps.");

                if (BootTvPath != null)
                    tmp = new Bitmap(BootTvPath);
                else
                    tmp = BootTvImg.Create();
                bootTvImg = new Bitmap(1280, 720, PixelFormat.Format24bppRgb);
                g = Graphics.FromImage(bootTvImg);
                g.DrawImage(tmp, new Rectangle(0, 0, 1280, 720));
                g.Dispose();
                tmp.Dispose();

                if (BootDrcPath != null)
                    tmp = new Bitmap(BootDrcPath);
                else
                    tmp = BootDrcImg.Create();
                bootDrcImg = new Bitmap(854, 480, PixelFormat.Format24bppRgb);
                g = Graphics.FromImage(bootDrcImg);
                g.DrawImage(tmp, new Rectangle(0, 0, 854, 480));
                g.Dispose();
                tmp.Dispose();

                if (IconPath != null)
                    tmp = new Bitmap(IconPath);
                else
                    tmp = IconImg.Create();
                iconImg = new Bitmap(128, 128, PixelFormat.Format32bppArgb);
                g = Graphics.FromImage(iconImg);
                g.DrawImage(tmp, new Rectangle(0, 0, 128, 128));
                g.Dispose();
                tmp.Dispose();

                Cll.Log.WriteLine("Bitmaps created.");
            }
            catch
            {
                Cll.Log.WriteLine("Error creating bitmaps.");
                return false;
            }

            if (!NusContent.ConvertToTGA(bootTvImg, currentDir + "/base/meta/bootTvTex.tga"))
            {
                Cll.Log.WriteLine("Error creating \"bootTvTex.tga\" file.");
                return false;
            }
            if (!NusContent.ConvertToTGA(bootDrcImg, currentDir + "/base/meta/bootDrcTex.tga"))
            {
                Cll.Log.WriteLine("Error creating \"bootDrcTex.tga\" file.");
                return false;
            }
            if (!NusContent.ConvertToTGA(iconImg, currentDir + "/base/meta/iconTex.tga"))
            {
                Cll.Log.WriteLine("Error creating \"iconTex.tga\" file.");
                return false;
            }

            Cll.Log.WriteLine("Injected TGA files.");

            return true;
        }

        private bool InjectMeta()
        {
            string titleId = TitleId;
            byte[] id = Useful.StrHexToByteArray(titleId, "");

            XmlWriterSettings xmlSettings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace
            };

            XmlDocument xmlApp = new XmlDocument();
            XmlDocument xmlMeta = new XmlDocument();

            try
            {
                Cll.Log.WriteLine("Editing \"app.xml\" and \"meta.xml\" files.");

                xmlApp.Load("base/code/app.xml");
                xmlMeta.Load("base/meta/meta.xml");

                XmlNode app_title_id = xmlApp.SelectSingleNode("app/title_id");
                XmlNode app_group_id = xmlApp.SelectSingleNode("app/group_id");

                XmlNode meta_product_code = xmlMeta.SelectSingleNode("menu/product_code");
                XmlNode meta_title_id = xmlMeta.SelectSingleNode("menu/title_id");
                XmlNode meta_group_id = xmlMeta.SelectSingleNode("menu/group_id");
                XmlNode meta_longname_ja = xmlMeta.SelectSingleNode("menu/longname_ja");
                XmlNode meta_longname_en = xmlMeta.SelectSingleNode("menu/longname_en");
                XmlNode meta_longname_fr = xmlMeta.SelectSingleNode("menu/longname_fr");
                XmlNode meta_longname_de = xmlMeta.SelectSingleNode("menu/longname_de");
                XmlNode meta_longname_it = xmlMeta.SelectSingleNode("menu/longname_it");
                XmlNode meta_longname_es = xmlMeta.SelectSingleNode("menu/longname_es");
                XmlNode meta_longname_zhs = xmlMeta.SelectSingleNode("menu/longname_zhs");
                XmlNode meta_longname_ko = xmlMeta.SelectSingleNode("menu/longname_ko");
                XmlNode meta_longname_nl = xmlMeta.SelectSingleNode("menu/longname_nl");
                XmlNode meta_longname_pt = xmlMeta.SelectSingleNode("menu/longname_pt");
                XmlNode meta_longname_ru = xmlMeta.SelectSingleNode("menu/longname_ru");
                XmlNode meta_longname_zht = xmlMeta.SelectSingleNode("menu/longname_zht");
                XmlNode meta_shortname_ja = xmlMeta.SelectSingleNode("menu/shortname_ja");
                XmlNode meta_shortname_en = xmlMeta.SelectSingleNode("menu/shortname_en");
                XmlNode meta_shortname_fr = xmlMeta.SelectSingleNode("menu/shortname_fr");
                XmlNode meta_shortname_de = xmlMeta.SelectSingleNode("menu/shortname_de");
                XmlNode meta_shortname_it = xmlMeta.SelectSingleNode("menu/shortname_it");
                XmlNode meta_shortname_es = xmlMeta.SelectSingleNode("menu/shortname_es");
                XmlNode meta_shortname_zhs = xmlMeta.SelectSingleNode("menu/shortname_zhs");
                XmlNode meta_shortname_ko = xmlMeta.SelectSingleNode("menu/shortname_ko");
                XmlNode meta_shortname_nl = xmlMeta.SelectSingleNode("menu/shortname_nl");
                XmlNode meta_shortname_pt = xmlMeta.SelectSingleNode("menu/shortname_pt");
                XmlNode meta_shortname_ru = xmlMeta.SelectSingleNode("menu/shortname_ru");
                XmlNode meta_shortname_zht = xmlMeta.SelectSingleNode("menu/shortname_zht");

                app_title_id.InnerText = titleId;
                app_group_id.InnerText = "0000" + id[5].ToString("X2") + id[6].ToString("X2");

                meta_product_code.InnerText = "WUP-N-" + Rom.ProductCode;
                meta_title_id.InnerText = titleId;
                meta_group_id.InnerText = "0000" + id[5].ToString("X2") + id[6].ToString("X2");
                meta_longname_ja.InnerText = LongName;
                meta_longname_en.InnerText = LongName;
                meta_longname_fr.InnerText = LongName;
                meta_longname_de.InnerText = LongName;
                meta_longname_it.InnerText = LongName;
                meta_longname_es.InnerText = LongName;
                meta_longname_zhs.InnerText = LongName;
                meta_longname_ko.InnerText = LongName;
                meta_longname_nl.InnerText = LongName;
                meta_longname_pt.InnerText = LongName;
                meta_longname_ru.InnerText = LongName;
                meta_longname_zht.InnerText = LongName;
                meta_shortname_ja.InnerText = ShortName;
                meta_shortname_en.InnerText = ShortName;
                meta_shortname_fr.InnerText = ShortName;
                meta_shortname_de.InnerText = ShortName;
                meta_shortname_it.InnerText = ShortName;
                meta_shortname_es.InnerText = ShortName;
                meta_shortname_zhs.InnerText = ShortName;
                meta_shortname_ko.InnerText = ShortName;
                meta_shortname_nl.InnerText = ShortName;
                meta_shortname_pt.InnerText = ShortName;
                meta_shortname_ru.InnerText = ShortName;
                meta_shortname_zht.InnerText = ShortName;

                XmlWriter app = XmlWriter.Create("base/code/app.xml", xmlSettings);
                XmlWriter meta = XmlWriter.Create("base/meta/meta.xml", xmlSettings);

                xmlApp.Save(app);
                xmlMeta.Save(meta);

                app.Close();
                meta.Close();

                Cll.Log.WriteLine("\"app.xml\" and \"meta.xml\" files editing successfully.");

                return true;
            }
            catch
            {
                Cll.Log.WriteLine("Error editing \"app.xml\" and \"meta.xml\" files.");
            }

            return false;
        }

        private bool InjectIni()
        {
            bool injected = true;

            try
            {
                Cll.Log.WriteLine("Empty \"base/content/config\" folder.");
                Directory.Delete("base/content/config", true);
                Directory.CreateDirectory("base/content/config");

                Cll.Log.WriteLine("Injecting INI data.");
                if (!IniIsLoaded)                    
                {
                    Cll.Log.WriteLine("Injecting an empty INI.");
                    File.Create("base/content/config/U" + Rom.ProductCodeVersion + ".z64.ini").Close();
                    Cll.Log.WriteLine("In: \"base/content/config/U" + Rom.ProductCodeVersion + ".z64.ini\"");
                    Cll.Log.WriteLine("INI injected.");
                }
                else if (VCN64ConfigFile.Copy(IniPath, "base/content/config/U" + Rom.ProductCodeVersion + ".z64.ini"))
                {
                    Cll.Log.WriteLine("CRC16: " + Ini.HashCRC16.ToString("X4"));
                    Cll.Log.WriteLine("In: \"base/content/config/U" + Rom.ProductCodeVersion + ".z64.ini\"");
                    Cll.Log.WriteLine("Injected INI.");
                }
                else
                {
                    Cll.Log.WriteLine("INI not injected, \"VCN64ConfigFile.Copy\" failed.");
                    injected = false;
                }
            }
            catch
            {
                Cll.Log.WriteLine("Error injecting INI.");
                injected = false;
            }

            return injected;
        }

        private bool InjectRom()
        {
            bool injected = true;

            try
            {
                Cll.Log.WriteLine("Empty \"base/content/rom\" folder.");
                Directory.Delete("base/content/rom", true);
                Directory.CreateDirectory("base/content/rom");

                Cll.Log.WriteLine("Injecting ROM.");
                if (RomN64.ToBigEndian(RomPath, "base/content/rom/U" + Rom.ProductCodeVersion + ".z64"))
                {
                    Cll.Log.WriteLine("CRC16: " + Rom.HashCRC16.ToString("X4"));
                    Cll.Log.WriteLine("In: \"base/content/rom/U" + Rom.ProductCodeVersion + ".z64\"");
                    Cll.Log.WriteLine("Injected ROM.");
                }
                else
                {
                    Cll.Log.WriteLine("ROM not injected, \"RomN64.ToBigEndian\" failed.");
                    injected = false;
                }
            }
            catch
            {
                Cll.Log.WriteLine("Error injecting ROM.");
                injected = false;
            }

            return injected;
        }

        #region Loads

        public bool LoadBase(string path)
        {
            if (Directory.Exists("base"))
            {                
                Directory.Delete("base", true);
                Cll.Log.WriteLine("Previous base deleted.");
            }

            if (IsValidBase(path))
            {
                Cll.Log.WriteLine("The \"" + path + "\" folder contains a valid base.");
                Useful.DirectoryCopy(path, "base", true);
            }
            else if (IsValidEncryptedBase(path))
            {
                Cll.Log.WriteLine("The \"" + path + "\" folder contains a valid encrypted base.");
                NusContent.Decrypt(path, "base");
            }
            else
                Cll.Log.WriteLine("The \"" + path + "\" folder not contains a valid base.");

            _base = GetLoadedBase();

            return BaseIsLoaded;
        }

        private VCN64 GetLoadedBase()
        {
            if (IsValidBase("base"))
            {
                FileStream fs = File.Open("base/code/VESSEL.rpx", FileMode.Open);
                uint hash = Cll.Security.ComputeCRC32(fs);
                fs.Close();

                switch (hash)
                {
                    case 0xFB245F10: return VCN64.Title01;
                    case 0x8EF60284: return VCN64.Title02;
                    case 0xF042E451: return VCN64.Title03;
                    case 0xAE933905: return VCN64.Title04;
                    case 0xCEB7A833: return VCN64.Title05;
                    case 0x7EB7B97D: return VCN64.Title06;
                    case 0x17BCC968: return VCN64.Title07;
                    case 0x05F20995: return VCN64.Title08;
                    case 0x8D3C196C: return VCN64.Title09;
                    case 0x307DCE21: return VCN64.Title10;
                    case 0xF41BC127: return VCN64.Title11;
                    case 0x36C0456E: return VCN64.Title12;
                    case 0x5559F831: return VCN64.Title13;
                    case 0xD554D2E4: return VCN64.Title14;
                    case 0x04F7D67F: return VCN64.Title15;
                    case 0xC376B949: return VCN64.Title16;
                    case 0xEE8855FF: return VCN64.Title17;
                    case 0x71FC1731: return VCN64.Title18;
                    case 0x967E7DF0: return VCN64.Title19;
                    case 0xBE3CEC5F: return VCN64.Title20;
                    case 0x89F2BC09: return VCN64.Title21;
                    case 0xFED1FB48: return VCN64.Title22;
                    case 0x724C4F5D: return VCN64.Title23;
                    case 0x2AF3C23B: return VCN64.Title24;
                    default:
                        Cll.Log.WriteLine("The base is valid but was not defined in the program code.");
                        return new VCN64(hash);
                }
            }
            else
                return null;
        }
        
        #endregion

        #region Validations

        private bool IsValidBase(string path)
        {
            bool valid = true;
            string[] folders = {
                path + "/content/config",
                path + "/content/rom"
            };
            string[] files = {
                path + "/code/app.xml",
                path + "/code/cos.xml",
                path + "/code/VESSEL.rpx",
                path + "/content/BuildInfo.txt",
                path + "/content/config.ini",
                path + "/content/FrameLayout.arc",
                path + "/meta/iconTex.tga",
                path + "/meta/bootTvTex.tga",
                path + "/meta/bootDrcTex.tga",
                path + "/meta/meta.xml"
            };

            foreach (string folder in folders)
            {
                if (!Directory.Exists(folder))
                {
                    valid = false;
                    Cll.Log.WriteLine("This folder is missing: \"" + folder + "\"");
                }
            }

            foreach (string file in files)
            {
                if (!File.Exists(file))
                {
                    valid = false;
                    Cll.Log.WriteLine("This file is missing: \"" + file + "\"");
                }
            }

            if (!valid)
                Cll.Log.WriteLine("The base is invalid.");

            return valid;
        }

        private bool IsValidEncryptedBase(string path)
        {
            string titleId = NusContent.CheckEncrypted(path);
            if (titleId != null &&
                NusContent.CheckCommonKeyFiles() &&
                File.Exists("resources/jnustool/JNUSTool.jar"))
            {
                string name = NusContent.JNUSToolWrapper(path, 400, 32768, titleId, "/code/cos.xml");

                if (name != null && File.Exists(name + "/code/cos.xml"))
                {
                    XmlDocument xmlCos = new XmlDocument();
                    xmlCos.Load(name + "/code/cos.xml");
                    XmlNode cos_argstr = xmlCos.SelectSingleNode("app/argstr");

                    Directory.Delete(name, true);

                    if (cos_argstr.InnerText == "VESSEL.rpx")
                        return true;
                    else
                    {
                        Cll.Log.WriteLine("\"" + path + "\" does not contain a N64 Wii U VC game.");
                        return false;
                    }
                }
                else if (name != null)
                {
                    Cll.Log.WriteLine("The NUS CONTENT does not contains \"cos.xml\" file.");
                    Directory.Delete(name, true);
                    return false;
                }
                else
                {
                    Cll.Log.WriteLine("JNUSToolWrapper could not decipher the NUS CONTENT.");
                    return false;
                }
            }
            else
            {
                Cll.Log.WriteLine("Some of the following files are missing:");
                Cll.Log.WriteLine(path + "/title.tmd");
                Cll.Log.WriteLine(path + "/title.tik");
                Cll.Log.WriteLine(path + "/title.cert");
                return false;
            }
        }

        #endregion
    }
}
