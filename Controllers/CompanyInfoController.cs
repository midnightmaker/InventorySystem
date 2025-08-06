using InventorySystem.Services;
using InventorySystem.ViewModels;
using InventorySystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Controllers
{
    public class CompanyInfoController : Controller
    {
        private readonly ICompanyInfoService _companyInfoService;
        private readonly ILogger<CompanyInfoController> _logger;

        public CompanyInfoController(ICompanyInfoService companyInfoService, ILogger<CompanyInfoController> logger)
        {
            _companyInfoService = companyInfoService;
            _logger = logger;
        }

        // GET: CompanyInfo
        public async Task<IActionResult> Index()
        {
            try
            {
                var companyInfo = await _companyInfoService.GetCompanyInfoAsync();
                return View(companyInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading company info");
                TempData["ErrorMessage"] = "Error loading company information.";
                return View();
            }
        }

        // GET: CompanyInfo/Edit
        public async Task<IActionResult> Edit()
        {
            try
            {
                var companyInfo = await _companyInfoService.GetCompanyInfoAsync();
                var viewModel = EditCompanyInfoViewModel.FromEntity(companyInfo);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading company info for editing");
                TempData["ErrorMessage"] = "Error loading company information for editing.";
                return RedirectToAction("Index");
            }
        }

        // POST: CompanyInfo/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditCompanyInfoViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                var companyInfo = viewModel.ToEntity();

                // Handle logo upload
                if (viewModel.LogoFile != null && viewModel.LogoFile.Length > 0)
                {
                    // Validate file type
                    var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
                    if (!allowedTypes.Contains(viewModel.LogoFile.ContentType.ToLower()))
                    {
                        ModelState.AddModelError("LogoFile", "Please upload a valid image file (JPEG, PNG, GIF, or WebP).");
                        return View(viewModel);
                    }

                    // Validate file size (max 5MB)
                    if (viewModel.LogoFile.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("LogoFile", "Logo file size must be less than 5MB.");
                        return View(viewModel);
                    }

                    // Read file data
                    using var memoryStream = new MemoryStream();
                    await viewModel.LogoFile.CopyToAsync(memoryStream);
                    companyInfo.LogoData = memoryStream.ToArray();
                    companyInfo.LogoContentType = viewModel.LogoFile.ContentType;
                    companyInfo.LogoFileName = viewModel.LogoFile.FileName;
                }
                else if (viewModel.RemoveExistingLogo)
                {
                    // Remove existing logo
                    companyInfo.LogoData = null;
                    companyInfo.LogoContentType = null;
                    companyInfo.LogoFileName = null;
                }

                await _companyInfoService.UpdateCompanyInfoAsync(companyInfo);

                TempData["SuccessMessage"] = "Company information updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating company info");
                ModelState.AddModelError("", "An error occurred while updating company information.");
                return View(viewModel);
            }
        }

        // GET: CompanyInfo/Logo/{id}
        public async Task<IActionResult> Logo(int id)
        {
            try
            {
                var companyInfo = await _companyInfoService.GetCompanyInfoAsync();
                
                if (companyInfo?.LogoData != null && companyInfo.LogoData.Length > 0)
                {
                    return File(companyInfo.LogoData, companyInfo.LogoContentType ?? "image/png");
                }
                
                // Return a default placeholder image or 404
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving company logo");
                return NotFound();
            }
        }

        // POST: CompanyInfo/RemoveLogo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveLogo()
        {
            try
            {
                var companyInfo = await _companyInfoService.GetCompanyInfoAsync();
                await _companyInfoService.RemoveCompanyLogoAsync(companyInfo.Id);
                
                TempData["SuccessMessage"] = "Company logo removed successfully!";
                return RedirectToAction("Edit");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing company logo");
                TempData["ErrorMessage"] = "Error removing company logo.";
                return RedirectToAction("Edit");
            }
        }
    }
}