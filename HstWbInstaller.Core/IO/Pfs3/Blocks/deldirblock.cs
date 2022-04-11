﻿namespace HstWbInstaller.Core.IO.Pfs3.Blocks
{
    using System;

    public class deldirblock : IBlock
    {
        // struct deldirblock
        // {
        //     UWORD id;				/* 'DD'								*/
        //     UWORD not_used;
        //     ULONG datestamp;
        //     ULONG seqnr;
        //     UWORD not_used_2[2];
        //     UWORD not_used_3;		/* roving in older versions	(<17.9)	*/	
        //     UWORD uid;				/* user id							*/
        //     UWORD gid;				/* group id							*/
        //     ULONG protection;
        //     UWORD creationday;
        //     UWORD creationminute;
        //     UWORD creationtick;
        //     struct deldirentry entries[0];	/* 31 entries				*/
        // };
        
        public ushort id { get; set; }
        public ushort not_used { get; set; }
        public uint datestamp { get; set; }
        public uint seqnr { get; set; }
        public ushort uid { get; set; }
        public ushort gid { get; set; }
        public uint protection { get; set; }
        public DateTime CreationDate { get; set; }
        public deldirentry[] entries { get; set; }

        public deldirblock()
        {
            id = Constants.DELDIRID;
        }
    }
}