﻿namespace HstWbInstaller.Core.IO.FastFileSystem
{
    using System;
    using System.Collections.Generic;

    public class RootBlock : EntryBlock
    {
	    
//         /*
// * Root block (BSIZE bytes) sector 880 for a DD disk, 1760 for a HD disk
// ------------------------------------------------------------------------------------------------
//         0/ 0x00	ulong	1	type		block primary type = T_HEADER (value 2)
//         4/ 0x04	ulong	1	header_key	unused in rootblock (value 0)
// 		ulong 	1 	high_seq	unused (value 0)
//        12/ 0x0c	ulong	1	ht_size		Hash table size in long (= BSIZE/4 - 56)
//                 	                        For floppy disk value 0x48
//        16/ 0x10	ulong	1	first_data	unused (value 0)
//        20/ 0x14	ulong	1	chksum		Rootblock checksum
//        24/ 0x18	ulong	*	ht[]		hash table (entry block number)
//         	                                * = (BSIZE/4) - 56
//                 	                        for floppy disk: size= 72 longwords
// BSIZE-200/-0xc8	ulong	1	bm_flag		bitmap flag, -1 means VALID
// BSIZE-196/-0xc4	ulong	25	bm_pages[]	bitmap blocks pointers (first one at bm_pages[0])
// BSIZE- 96/-0x60	ulong	1	bm_ext		first bitmap extension block
// 						(Hard disks only)
// BSIZE- 92/-0x5c	ulong 	1 	r_days		last root alteration date : days since 1 jan 78
// BSIZE- 88/-0x58	ulong 	1 	r_mins 		minutes past midnight
// BSIZE- 84/-0x54	ulong 	1 	r_ticks 	ticks (1/50 sec) past last minute
// BSIZE- 80/-0x50	char	1	name_len	volume name length
// BSIZE- 79/-0x4f	char	30	diskname[]	volume name
// BSIZE- 49/-0x31	char	1	UNUSED		set to 0
// BSIZE- 48/-0x30	ulong	2	UNUSED		set to 0
// BSIZE- 40/-0x28	ulong	1	v_days		last disk alteration date : days since 1 jan 78
// BSIZE- 36/-0x24	ulong	1	v_mins		minutes past midnight
// BSIZE- 32/-0x20	ulong	1	v_ticks		ticks (1/50 sec) past last minute
// BSIZE- 28/-0x1c	ulong	1	c_days		filesystem creation date
// BSIZE- 24/-0x18	ulong	1	c_mins 		
// BSIZE- 20/-0x14	ulong	1	c_ticks
// 		ulong	1	next_hash	unused (value = 0)
// 		ulong	1	parent_dir	unused (value = 0)
// BSIZE-  8/-0x08	ulong	1	extension	FFS: first directory cache block,
// 						0 otherwise
// BSIZE-  4/-0x04	ulong	1	sec_type	block secondary type = ST_ROOT 
// 						(value 1)
// 						
// 						
// struct bRootBlock {
// /*000*/	int32_t	type;
//         int32_t	headerKey;
//         int32_t	highSeq;
// /*00c*/	int32_t	hashTableSize;
//         int32_t	firstData;
// /*014*/	ULONG	checkSum;
// /*018*/	int32_t	hashTable[HT_SIZE];		/* hash table */
// /*138*/	int32_t	bmFlag;				/* bitmap flag, -1 means VALID */
// /*13c*/	int32_t	bmPages[BM_SIZE];
// /*1a0*/	int32_t	bmExt;
// /*1a4*/	int32_t	cDays; 	/* creation date FFS and OFS */
// /*1a8*/	int32_t	cMins;
// /*1ac*/	int32_t	cTicks;
// /*1b0*/	char	nameLen;
// /*1b1*/	char 	diskName[MAXNAMELEN+1];
//         char	r2[8];
// /*1d8*/	int32_t	days;		/* last access : days after 1 jan 1978 */
// /*1dc*/	int32_t	mins;		/* hours and minutes in minutes */
// /*1e0*/	int32_t	ticks;		/* 1/50 seconds */
// /*1e4*/	int32_t	coDays;	/* creation date OFS */
// /*1e8*/	int32_t	coMins;
// /*1ec*/	int32_t	coTicks;
//         int32_t	nextSameHash;	/* == 0 */
//         int32_t	parent;		/* == 0 */
// /*1f8*/	int32_t	extension;		/* FFS: first directory cache block */
// /*1fc*/	int32_t	secType;	/* == 1 */
//     };						
//------------------------------------------------------------------------------------------------         */
        
        public uint Offset { get; set; }
        // public byte[] BlockBytes { get; set; }
        //
        // public int Type { get; set; }
        // public int HeaderKey { get; set; }
        // public int HighSeq { get; set; }
        // public int HashtableSize { get; set; }
        // public int FirstData { get; set; }
        // public int Checksum { get; set; }
        
        public int BitmapFlags { get; set; }
        public int[] BitmapBlockOffsets { get; set; } // bmPages
        public uint BitmapBlocksOffset { get; set; }
        
        /// <summary>
        /// first bitmap extension block (when there's more than 25 bitmap blocks)
        /// </summary>
        public uint BitmapExtensionBlocksOffset { get; set; }
        
        public string DiskName { get; set; }
        public DateTime RootAlterationDate { get; set; }
        public DateTime DiskAlterationDate { get; set; }
        public DateTime FileSystemCreationDate { get; set; }
        
        // FFS: first directory cache block, 0 otherwise
//        public uint FirstDirectoryCacheBlock { get; set; }
        //
        // public int NextSameHash { get; set; }
        // public int Parent { get; set; }
        // //public uint ExtensionBlockOffset { get; set; }
        // public int Extension
        // {
        //     get => 0;
        //     set { }
        // }
        // public int SecType { get; set; }
        //
        //
        
        public IEnumerable<BitmapBlock> BitmapBlocks { get; set; }
        public IEnumerable<BitmapExtensionBlock> BitmapExtensionBlocks { get; set; }

        public int[] bmPages => BitmapBlockOffsets;

        public RootBlock()
        {
            Type = 2;
            HeaderKey = 0;
            HighSeq = 0;
            HashTableSize = Constants.HT_SIZE;
            FirstData = 0;
            Checksum = 0;
            HashTable = new int[Constants.HT_SIZE];
            
            BitmapFlags = -1;

            var now = DateTime.UtcNow;
            RootAlterationDate = now;
            DiskAlterationDate = now;
            FileSystemCreationDate = now;

            Extension = 0;
            SecType = Constants.ST_ROOT;
            
            BitmapBlocks = new List<BitmapBlock>();
            BitmapExtensionBlocks = new List<BitmapExtensionBlock>();

            //bmPages = new int[Constants.BM_SIZE];
        }
    }
}