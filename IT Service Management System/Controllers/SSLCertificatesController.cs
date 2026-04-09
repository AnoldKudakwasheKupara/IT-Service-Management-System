using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class SSLCertificatesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SSLCertificatesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var certs = await _context.SSLCertificates
                .OrderBy(c => c.ExpiryDate)
                .ToListAsync();

            return View(certs);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var cert = await _context.SSLCertificates
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cert == null)
                return NotFound();

            return View(cert);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SSLCertificate cert)
        {
            if (ModelState.IsValid)
            {
                _context.Add(cert);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(cert);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var cert = await _context.SSLCertificates.FindAsync(id);

            if (cert == null)
                return NotFound();

            return View(cert);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SSLCertificate cert)
        {
            if (id != cert.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingCert = await _context.SSLCertificates
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == id);

                    if (existingCert == null)
                        return NotFound();

                    // 🔥 Detect if renewal checkbox was just ticked
                    if (!existingCert.IsRenewed && cert.IsRenewed)
                    {
                        cert.LastRenewedDate = DateTime.Now;

                        // ✅ Reset expiry to 1 year from now
                        cert.ExpiryDate = DateTime.Now.AddYears(1);
                    }

                    _context.Update(cert);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.SSLCertificates.Any(e => e.Id == cert.Id))
                        return NotFound();
                    else
                        throw;
                }

                return RedirectToAction(nameof(Index));
            }

            return View(cert);
        }
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var cert = await _context.SSLCertificates
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cert == null)
                return NotFound();

            return View(cert);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cert = await _context.SSLCertificates.FindAsync(id);

            if (cert != null)
            {
                _context.SSLCertificates.Remove(cert);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ExpiringSoon()
        {
            var threshold = DateTime.Now.AddDays(30);

            var certs = await _context.SSLCertificates
                .Where(c => c.ExpiryDate <= threshold)
                .OrderBy(c => c.ExpiryDate)
                .ToListAsync();

            return View("Index", certs);
        }

        public async Task<IActionResult> Expired()
        {
            var now = DateTime.Now;

            var certs = await _context.SSLCertificates
                .Where(c => c.ExpiryDate < now)
                .OrderBy(c => c.ExpiryDate)
                .ToListAsync();

            return View("Index", certs);
        }

        private bool SSLCertificateExists(int id)
        {
            return _context.SSLCertificates.Any(e => e.Id == id);
        }
    }
}