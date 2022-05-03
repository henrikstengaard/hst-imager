﻿namespace HstWbInstaller.Core.IO.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    public static class Directory
    {
        // https://github.com/lclevy/ADFlib/blob/be8a6f6e8d0ca8fda963803eef77366c7584649a/src/adf_dir.c#L362
        public static async Task<IEnumerable<Entry>> AdfGetRDirEnt(Volume volume, IEntryBlock startEntryBlock,
            bool recursive = false)
        {
            // struct bEntryBlock entryBlk;
            // struct List* cell,  *head;
            // int i;
            // struct Entry* entry;
            // SECTNUM nextSector;
            // int32_t* hashTable;
            // struct bEntryBlock parent;
            //
            //
            // if (adfEnv.useDirCache && isDIRCACHE(vol->dosType))
            //     return (adfGetDirEntCache(vol, nSect, recurs));
            //
            // reads sector "nSect" to parent
            // if (adfReadEntryBlock(vol, nSect, &parent) != RC_OK)
            //     return NULL;

            // if (adfEnv.useDirCache && isDIRCACHE(vol->dosType))
            //     return (adfGetDirEntCache(vol, nSect, recurs));

            // if (adfEnv.useDirCache && isDIRCACHE(vol->dosType))
            //     return (adfGetDirEntCache(vol, nSect, recurs));
            if (Macro.isDIRCACHE(volume.DosType))
            {
                //     return (adfGetDirEntCache(vol, nSect, recurs));
            }

            var hashTable = startEntryBlock.HashTable.ToList();
            //cell = head = NULL;
            var entries = new List<Entry>();

            for (var i = 0; i < Constants.HT_SIZE; i++)
            {
                if (hashTable[i] == 0)
                {
                    continue;
                }

                if (hashTable[i] < volume.FirstBlock || hashTable[i] > volume.LastBlock)
                {
                    continue;
                }

                //         if (adfReadEntryBlock(vol, hashTable[i], &entryBlk)!=RC_OK) {
                // adfFreeDirList(head);
                //             return NULL;
                //         }
                var entryBlock = await Disk.AdfReadEntryBlock(volume, hashTable[i]);

                //         if (adfEntBlock2Entry(&entryBlk, entry)!=RC_OK) {
                // adfFreeDirList(head); return NULL;
                var entry = AdfEntBlock2Entry(entryBlock);
                entry.Sector = hashTable[i];

                entries.Add(entry);

                //         if (recurs && entry->type==ST_DIR)
                //             cell->subdir = adfGetRDirEnt(vol,entry->sector,recurs);
                if (recursive && entry.Type == Entry.EntryType.Dir)
                {
                    entry.SubDir = (await AdfGetRDirEnt(volume, entryBlock, true)).ToList();
                }

                //         /* same hashcode linked list */
                //         nextSector = entryBlk.nextSameHash;
                //         while( nextSector!=0 ) {
                var nextSector = entryBlock.NextSameHash;
                while (nextSector != 0)
                {
                    //             if (adfReadEntryBlock(vol, nextSector, &entryBlk)!=RC_OK) {
                    //  adfFreeDirList(head); return NULL;
                    //             }
                    entryBlock = await Disk.AdfReadEntryBlock(volume, nextSector);

                    //             if (adfEntBlock2Entry(&entryBlk, entry)!=RC_OK) {
                    //  adfFreeDirList(head);
                    //                 return NULL;
                    //             }
                    //             entry->sector = nextSector;
                    entry = AdfEntBlock2Entry(entryBlock);
                    entry.Sector = nextSector;

                    //             if (recurs && entry->type==ST_DIR)
                    //                 cell->subdir = adfGetRDirEnt(vol,entry->sector,recurs);
                    if (recursive && entry.Type == Entry.EntryType.Dir)
                    {
                        entry.SubDir = (await AdfGetRDirEnt(volume, entryBlock, true)).ToList();
                    }

                    nextSector = entryBlock.NextSameHash;
                }
            }

            return entries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryBlock"></param>
        /// <returns></returns>
        /// <exception cref="IOException"></exception>
        private static Entry AdfEntBlock2Entry(EntryBlock entryBlock)
        {
            // AdfEntBlock2Entry
            // https://github.com/lclevy/ADFlib/blob/be8a6f6e8d0ca8fda963803eef77366c7584649a/src/adf_dir.c#L532
            var entry = new Entry
            {
                Parent = entryBlock.Parent,
                Name = entryBlock.Name,
                Comment = string.Empty,
                Date = entryBlock.Date,
                Access = -1,
                Size = 0,
                Real = 0,
                EntryBlock = entryBlock
            };

            switch (entryBlock.SecType)
            {
                case Constants.ST_ROOT:
                    entry.Type = Entry.EntryType.Root;
                    break;
                case Constants.ST_DIR:
                    entry.Type = Entry.EntryType.Dir;
                    entry.Access = entryBlock.Access;
                    entry.Comment = entryBlock.Comment;
                    break;
                case Constants.ST_FILE:
                    entry.Type = Entry.EntryType.File;
                    entry.Access = entryBlock.Access;
                    entry.Size = entryBlock.ByteSize;
                    entry.Comment = entryBlock.Comment;
                    break;
                case Constants.ST_LFILE:
                    entry.Type = Entry.EntryType.LFile;
                    entry.Real = entryBlock.RealEntry;
                    break;
                case Constants.ST_LDIR:
                    entry.Type = Entry.EntryType.LDir;
                    entry.Real = entryBlock.RealEntry;
                    break;
                case Constants.ST_LSOFT:
                    entry.Type = Entry.EntryType.LSoft;
                    break;
                default:
                    throw new IOException("unknown entry type");
            }

            return entry;
        }

        public static char AdfToUpper(char c)
        {
            return (char)(c >= 'a' && c <= 'z' ? c - ('a' - 'A') : c);
        }

        /*
 * adfIntlToUpper
 *
 */
        public static char AdfIntlToUpper(char c)
        {
            return (char)((c >= 'a' && c <= 'z') || (c >= 224 && c <= 254 && c != 247) ? c - ('a' - 'A') : c);
        }

/*
 * adfGetHashValue
 * 
 */
        public static int AdfGetHashValue(string name, bool intl)
        {
            var hash = (uint)name.Length;
            foreach (var c in name)
            {
                var upper = intl ? AdfIntlToUpper(c) : AdfToUpper(c);
                hash = (hash * 13 + upper) & 0x7ff;
            }

            hash %= Constants.HT_SIZE;
            return (int)hash;
        }

/*
 * myToUpper
 *
 */
        public static string MyToUpper(string str, bool intl)
        {
            var nstr = str.ToCharArray();
            for (var i = 0; i < str.Length; i++)
            {
                nstr[i] = intl ? AdfIntlToUpper(str[i]) : AdfToUpper(str[i]);
            }

            return new string(nstr);
        }

/*
 * adfNameToEntryBlk
 *
 */
        public static async Task<NameToEntryBlockResult> AdfNameToEntryBlk(Volume vol, int[] ht, string name,
            bool nUpdSect)
        {
            // uint8_t upperName[MAXNAMELEN+1];
            // uint8_t upperName2[MAXNAMELEN+1];
            // SECTNUM nSect;
            // BOOL found;
            // SECTNUM updSect;

            var intl = Macro.isINTL(vol.DosType) || Macro.isDIRCACHE(vol.DosType);
            var hashVal = AdfGetHashValue(name, intl);
            var nameLen = Math.Min(name.Length, Constants.MAXNAMELEN);
            var upperName = MyToUpper(name, intl);

            var nSect = ht[hashVal];
/*printf("name=%s ht[%d]=%d upper=%s len=%d\n",name,hashVal,nSect,upperName,nameLen);
printf("hashVal=%d\n",adfGetHashValue(upperName, intl ));
if (!strcmp("españa.country",name)) {
int i;
for(i=0; i<HT_SIZE; i++) printf("ht[%d]=%d    ",i,ht[i]);
}*/
            if (nSect == 0)
                return new NameToEntryBlockResult
                {
                    NSect = -1
                };

            EntryBlock entry;
            var updSect = 0;
            var found = false;
            do
            {
                entry = await Disk.AdfReadEntryBlock(vol, nSect);
                if (entry == null)
                {
                    return new NameToEntryBlockResult
                    {
                        NSect = -1
                    };
                }
                // if (Disk.AdfReadEntryBlock(vol, nSect, entry) != RC_OK)
                //     return -1;
                if (nameLen == entry.Name.Length)
                {
                    var upperName2 = MyToUpper(entry.Name, intl);
/*printf("2=%s %s\n",upperName2,upperName);*/
                    found = upperName == upperName2;
                }

                if (!found)
                {
                    updSect = nSect;
                    nSect = entry.NextSameHash;
                }
            } while (!found && nSect != 0);

            if (nSect == 0 && !found)
                return new NameToEntryBlockResult
                {
                    NSect = -1,
                };
            
            return new NameToEntryBlockResult
            {
                NSect = nSect,
                EntryBlock = entry,
                NUpdSect = nUpdSect ? updSect : null
            };
        }
        
        /*
 * adfCreateFile
 *
 */
        public static async Task<FileHeaderBlock> AdfCreateFile(Volume vol, int nParent, string name)
        {
            // SECTNUM nSect;
            // struct bEntryBlock parent;
/*puts("adfCreateFile in");*/
            var parent = await Disk.AdfReadEntryBlock(vol, nParent);
            // if (Disk.AdfReadEntryBlock(vol, nParent, &parent)!=RC_OK)
            //     return RC_ERROR;

            /* -1 : do not use a specific, already allocated sector */
            var nSect = AdfCreateEntry(vol, parent, name, -1);
            if (nSect == -1) throw new IOException("error nSect is -1");
/*printf("new fhdr=%d\n",nSect);*/
            //memset(fhdr,0,512);

            // if (parent.SecType==Constants.ST_ROOT)
            //     fhdr->parent = vol->rootBlock;
            // else if (parent.SecType==ST_DIR)
            //     fhdr->parent = parent.headerKey;
            // else
            //     (*adfEnv.wFct)("adfCreateFile : unknown parent secType");

            if (!(parent.SecType == Constants.ST_ROOT || parent.SecType == Constants.ST_DIR))
            {
                throw new IOException("adfCreateFile : unknown parent secType");
            }
            
            var fhdr = new FileHeaderBlock
            {
                HeaderKey = nSect,
                fileName = name,
                parent = parent.SecType == Constants.ST_ROOT ? (int)vol.RootBlock.Offset : parent.HeaderKey,
                Date = DateTime.Now
            };
            
            // fhdr->nameLen = Math.Min(Constants.MAXNAMELEN, name.Length);
            // memcpy(fhdr->fileName,name,fhdr->nameLen);
            // fhdr->headerKey = nSect;
            // if (parent.secType==ST_ROOT)
            //     fhdr->parent = vol->rootBlock;
            // else if (parent.secType==ST_DIR)
            //     fhdr->parent = parent.headerKey;
            // else
            //     (*adfEnv.wFct)("adfCreateFile : unknown parent secType");
            // adfTime2AmigaTime(adfGiveCurrentTime(),
            //     &(fhdr->days),&(fhdr->mins),&(fhdr->ticks));

            // if (adfWriteFileHdrBlock(vol,nSect,fhdr)!=RC_OK)
            //     return RC_ERROR;
            await File.AdfWriteFileHdrBlock(vol, nSect, fhdr);

            if (Macro.isDIRCACHE(vol.DosType))
            {
                // adfAddInCache(vol, &parent, (struct bEntryBlock *)fhdr);
                Cache.AdfAddInCache(vol, parent, fhdr);
            }

            Bitmap.AdfUpdateBitmap(vol);

            // if (adfEnv.useNotify)
            //     (*adfEnv.notifyFct)(nParent,ST_FILE);
            //
            // return RC_OK;
            return fhdr;
        }

        public static int AdfCreateEntry(Volume vol, IEntryBlock dir, string name, int thisSect)
        {
            throw new NotImplementedException();
        }
    }
}