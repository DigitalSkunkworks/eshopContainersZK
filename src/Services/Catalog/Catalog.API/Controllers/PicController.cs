using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using zipkin4net;

// For more information on enabling MVC for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Controllers
{
    public class PicController : Controller
    {
        private readonly IHostingEnvironment _env;
        private readonly CatalogContext _catalogContext;
        private zipkin4net.Trace trace;
        public PicController()
        {
            trace = zipkin4net.Trace.Create();
        }

        public PicController(IHostingEnvironment env,
            CatalogContext catalogContext)
        {
            trace = zipkin4net.Trace.Create();
            _env = env;
            _catalogContext = catalogContext;
        }

        [HttpGet]
        [Route("api/v1/catalog/items/{catalogItemId:int}/pic")]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        // GET: /<controller>/
        public async Task<IActionResult> GetImage(int catalogItemId)
        {
            trace.Record(Annotations.ServiceName("PicController:GetImage"));
            trace.Record(Annotations.ServerRecv());

            if (catalogItemId <= 0)
            {
                trace.Record(Annotations.ServerSend());
                return BadRequest();
            }

            var item = await _catalogContext.CatalogItems
                .SingleOrDefaultAsync(ci => ci.Id == catalogItemId);

            if (item != null)
            {
                var webRoot = _env.WebRootPath;
                var path = Path.Combine(webRoot, item.PictureFileName);

                string imageFileExtension = Path.GetExtension(item.PictureFileName);
                string mimetype = GetImageMimeTypeFromImageFileExtension(imageFileExtension);

                var buffer = System.IO.File.ReadAllBytes(path);

                trace.Record(Annotations.ServerSend());
                return File(buffer, mimetype);
            }

            trace.Record(Annotations.ServerSend());
            return NotFound();
        }

        private string GetImageMimeTypeFromImageFileExtension(string extension)
        {
            trace.Record(Annotations.LocalOperationStart("GetImageMimeTypeFromImageFileExtension"));
            string mimetype;

            switch (extension)
            {
                case ".png":
                    mimetype = "image/png";
                    break;
                case ".gif":
                    mimetype = "image/gif";
                    break;
                case ".jpg":
                case ".jpeg":
                    mimetype = "image/jpeg";
                    break;
                case ".bmp":
                    mimetype = "image/bmp";
                    break;
                case ".tiff":
                    mimetype = "image/tiff";
                    break;
                case ".wmf":
                    mimetype = "image/wmf";
                    break;
                case ".jp2":
                    mimetype = "image/jp2";
                    break;
                case ".svg":
                    mimetype = "image/svg+xml";
                    break;
                default:
                    mimetype = "application/octet-stream";
                    break;
            }
            trace.Record(Annotations.LocalOperationStop());

            return mimetype;
        }
    }
}
