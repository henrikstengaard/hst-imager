using System;
using System.IO;
using System.Threading.Tasks;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.PhysicalDrives;
using Xunit;

namespace Hst.Imager.Core.Tests.PhysicalDriveManagerTests;

public class GenericPhysicalDriveTests
{
    /// <summary>
    /// Tests stream is disposed when exiting using scope of generic physical drive.
    /// The unit test is created based on testing hst imager console in linux formatting a physical drive,
    /// which resulted in "process cannot access the file /dev/sdx because it's being used by another process"
    /// exception as the stream was not disposed properly between getting media for the physical drive to format.
    /// </summary>
    [Fact]
    public async Task When_ExitingGenericPhysicalDriveUsingScope_Then_StreamIsDisposed()
    {
        // arrange - physical drive path, name and size
        const string name = "physical-drive";
        const string type = "disk";
        var path = $"{Guid.NewGuid()}.img";
        const int size = 1024;

        var data = new byte[size];
        Array.Fill<byte>(data, 1, 0, 512);
        Array.Fill<byte>(data, 2, 512, 512);
        
        var dataReadFromMedia = new byte[size];
        var dataReadFromPhysicalDrive = new byte[size];
        
        try
        {
            // arrange - create a file to simulate a physical drive
            await File.WriteAllBytesAsync(path, data);
        
            // arrange - create generic physical drive
            using (var genericPhysicalDrive = new GenericPhysicalDrive(path, type, name, size))
            {
                int bytesRead;
                
                // act - create physical drive media opening and reading from physical drive path
                using(var media = new PhysicalDriveMedia(path, name, size, 
                          Media.MediaType.Raw, true, genericPhysicalDrive, false))        
                {
                    // act - read from physical drive media stream
                    media.Stream.Seek(0, SeekOrigin.Begin);
                    bytesRead = await media.Stream.ReadAsync(dataReadFromMedia, 0, dataReadFromMedia.Length);
                    
                    // assert - read 1024 bytes
                    Assert.Equal(size, bytesRead);
                }
                
                // act - open generic physical drive stream
                var stream = genericPhysicalDrive.Open(false, CacheType.Memory, 1024 * 1024);

                // act - read from generic physical drive stream
                stream.Seek(0, SeekOrigin.Begin);
                bytesRead = await stream.ReadAsync(dataReadFromPhysicalDrive, 0, dataReadFromPhysicalDrive.Length);
                
                // assert - read 1024 bytes
                Assert.Equal(size, bytesRead);
            }

            // assert - no exceptions are thrown when opening file stream to physical drive path since stream is closed
            var exception = await Record.ExceptionAsync(async () =>
            {
                await using var fileStream = File.OpenRead(path);
            });
            Assert.Null(exception);
            
            // assert - data read from media and physical drive are equal
            Assert.Equal(data, dataReadFromMedia);
            Assert.Equal(data, dataReadFromPhysicalDrive);
        }
        finally
        {
            TestHelper.DeletePaths(path);
        }
    }
}