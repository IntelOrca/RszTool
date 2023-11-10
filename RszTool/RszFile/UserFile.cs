namespace RszTool
{
    public class UserFile : BaseRszFile
    {
        public struct HeaderStruct {
            public uint magic;
            public int resourceCount;
            public int userdataCount;
            public int infoCount;
            public long resourceInfoOffset;
            public long userdataInfoOffset;
            public long dataOffset;
        };

        public StructModel<HeaderStruct> Header = new();
        public List<ResourceInfo> ResourceInfoList = new();
        public List<UserdataInfo> UserdataInfoList = new();
        public RSZFile? RSZ { get; private set; }


        public UserFile(RszFileOption option, FileHandler fileHandler) : base(option, fileHandler)
        {
        }

        public const uint Magic = 0x525355;
        public const string Extension = ".2";
        public const string Extension2 = ".user";

        protected override bool DoRead()
        {
            var handler = FileHandler;
            if (!Header.Read(handler)) return false;
            if (Header.Data.magic != Magic)
            {
                throw new InvalidDataException($"{handler.FilePath} Not a SCN file");
            }

            handler.Seek(Header.Data.dataOffset);
            ResourceInfoList.Read(handler, Header.Data.resourceCount);

            handler.Seek(Header.Data.userdataInfoOffset);
            UserdataInfoList.Read(handler, Header.Data.userdataCount);

            RSZ = new RSZFile(Option, FileHandler.WithOffset(Header.Data.dataOffset));
            RSZ.Read(0, false);
            return true;
        }

        protected override bool DoWrite()
        {
            var handler = FileHandler;

            handler.Seek(Header.Size);
            handler.Align(16);
            Header.Data.resourceInfoOffset = handler.Tell();
            ResourceInfoList.Write(handler);

            handler.Align(16);
            Header.Data.userdataInfoOffset = handler.Tell();
            UserdataInfoList.Write(handler);

            handler.FlushStringToWrite();

            Header.Data.dataOffset = handler.Tell();
            // 内部偏移是从0开始算的
            RSZ!.WriteTo(FileHandler.WithOffset(Header.Data.dataOffset));

            Header.Data.resourceCount = ResourceInfoList.Count;
            Header.Data.userdataCount = UserdataInfoList.Count;
            Header.Rewrite(handler);
            return true;
        }
    }
}
