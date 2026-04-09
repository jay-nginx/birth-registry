using BirthRegistry.Models;
using BirthRegistry.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BirthRegistry.Functions;

/// <summary>
/// Serves the HTML registration form and handles form submissions.
/// GET  /api/register  → render the form
/// POST /api/register  → submit form data and register the birth
/// </summary>
public class RegistrationFormFunction(IBirthRegistryService service, ILogger<RegistrationFormFunction> logger)
{
    [Function("GetRegistrationForm")]
    public IActionResult GetForm(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "register")] HttpRequest req)
    {
        logger.LogInformation("Serving birth registration form");
        return new ContentResult
        {
            Content = HtmlTemplates.RegistrationForm(null, null),
            ContentType = "text/html",
            StatusCode = 200
        };
    }

    [Function("SubmitRegistrationForm")]
    public async Task<IActionResult> SubmitForm(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "register")] HttpRequest req)
    {
        logger.LogInformation("Processing birth registration form submission");

        try
        {
            var form = await req.ReadFormAsync();
            var dto = new BirthRecordDto
            {
                ChildFirstName  = form["ChildFirstName"].ToString().Trim(),
                ChildLastName   = form["ChildLastName"].ToString().Trim(),
                DateOfBirth     = form["DateOfBirth"].ToString().Trim(),
                Gender          = form["Gender"].ToString().Trim(),
                HospitalName    = form["HospitalName"].ToString().Trim(),
                CityOfBirth     = form["CityOfBirth"].ToString().Trim(),
                CountryOfBirth  = form["CountryOfBirth"].ToString().Trim(),
                BirthWeightKg   = form["BirthWeightKg"].ToString().Trim(),
                FatherFirstName = form["FatherFirstName"].ToString().Trim(),
                FatherLastName  = form["FatherLastName"].ToString().Trim(),
                MotherFirstName = form["MotherFirstName"].ToString().Trim(),
                MotherMaidenName = form["MotherMaidenName"].ToString().Trim(),
                ParentAddress   = form["ParentAddress"].ToString().Trim(),
                ParentPostcode  = form["ParentPostcode"].ToString().Trim(),
                ContactPhone    = form["ContactPhone"].ToString().Trim(),
                ContactEmail    = form["ContactEmail"].ToString().Trim(),
                Notes           = form["Notes"].ToString().Trim()
            };

            var errors = ValidateDto(dto);
            if (errors.Count > 0)
            {
                return new ContentResult
                {
                    Content = HtmlTemplates.RegistrationForm(dto, errors),
                    ContentType = "text/html",
                    StatusCode = 400
                };
            }

            var record = await service.RegisterBirthAsync(dto);
            logger.LogInformation("Birth registered: {RegNo}", record.RegistrationNumber);

            return new ContentResult
            {
                Content = HtmlTemplates.RegistrationSuccess(record),
                ContentType = "text/html",
                StatusCode = 200
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register birth");
            return new ContentResult
            {
                Content = HtmlTemplates.ErrorPage("An unexpected error occurred. Please try again."),
                ContentType = "text/html",
                StatusCode = 500
            };
        }
    }

    private static List<string> ValidateDto(BirthRecordDto dto)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.ChildFirstName))  errors.Add("Child's first name is required.");
        if (string.IsNullOrWhiteSpace(dto.ChildLastName))   errors.Add("Child's last name is required.");
        if (string.IsNullOrWhiteSpace(dto.DateOfBirth) || !DateTime.TryParse(dto.DateOfBirth, out _))
            errors.Add("A valid date of birth is required.");
        if (string.IsNullOrWhiteSpace(dto.Gender))          errors.Add("Gender is required.");
        if (string.IsNullOrWhiteSpace(dto.FatherFirstName)) errors.Add("Father's first name is required.");
        if (string.IsNullOrWhiteSpace(dto.FatherLastName))  errors.Add("Father's last name is required.");
        if (string.IsNullOrWhiteSpace(dto.MotherFirstName)) errors.Add("Mother's first name is required.");
        if (string.IsNullOrWhiteSpace(dto.MotherMaidenName)) errors.Add("Mother's maiden name is required.");
        if (string.IsNullOrWhiteSpace(dto.ParentAddress))   errors.Add("Parent address is required.");
        return errors;
    }
}
