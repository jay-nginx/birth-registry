using BirthRegistry.Models;

namespace BirthRegistry.Functions;

internal static class HtmlTemplates
{
    private const string Layout = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1.0" />
          <title>{TITLE} — Birth Registry</title>
          <style>
            *, *::before, *::after { box-sizing: border-box; }
            body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
                   background: #f0f4f8; color: #1a202c; margin: 0; padding: 0; }
            header { background: #2b6cb0; color: #fff; padding: 1rem 2rem;
                     display: flex; align-items: center; gap: 2rem; }
            header h1 { margin: 0; font-size: 1.4rem; }
            header nav a { color: #bee3f8; text-decoration: none; margin-right: 1.2rem; }
            header nav a:hover { color: #fff; }
            main { max-width: 860px; margin: 2rem auto; padding: 0 1rem; }
            .card { background: #fff; border-radius: 8px; box-shadow: 0 1px 4px rgba(0,0,0,.1);
                    padding: 2rem; margin-bottom: 1.5rem; }
            h2 { margin-top: 0; color: #2b6cb0; }
            .section-title { font-size: .75rem; font-weight: 700; text-transform: uppercase;
                             letter-spacing: .08em; color: #718096; margin: 1.5rem 0 .5rem; border-bottom: 1px solid #e2e8f0; padding-bottom: .25rem; }
            .grid { display: grid; grid-template-columns: 1fr 1fr; gap: .75rem 1.5rem; }
            .full { grid-column: 1 / -1; }
            label { display: block; font-size: .85rem; font-weight: 600; margin-bottom: .25rem; }
            input, select, textarea {
              width: 100%; padding: .5rem .75rem; border: 1px solid #cbd5e0;
              border-radius: 6px; font-size: .95rem; background: #fff; }
            input:focus, select:focus, textarea:focus {
              outline: none; border-color: #4299e1; box-shadow: 0 0 0 3px rgba(66,153,225,.2); }
            textarea { resize: vertical; min-height: 80px; }
            .btn { display: inline-block; padding: .6rem 1.4rem; border-radius: 6px; font-size: .95rem;
                   font-weight: 600; cursor: pointer; border: none; text-decoration: none; }
            .btn-primary { background: #2b6cb0; color: #fff; }
            .btn-primary:hover { background: #2c5282; }
            .btn-secondary { background: #e2e8f0; color: #2d3748; }
            .btn-secondary:hover { background: #cbd5e0; }
            .alert { padding: .85rem 1rem; border-radius: 6px; margin-bottom: 1rem; }
            .alert-error { background: #fff5f5; border: 1px solid #fc8181; color: #c53030; }
            .alert-success { background: #f0fff4; border: 1px solid #68d391; color: #276749; }
            .badge { display: inline-block; padding: .2rem .6rem; border-radius: 999px;
                     font-size: .75rem; font-weight: 700; }
            .badge-green { background: #c6f6d5; color: #276749; }
            .badge-blue  { background: #bee3f8; color: #2b6cb0; }
            .badge-yellow { background: #fefcbf; color: #744210; }
            .badge-red   { background: #fed7d7; color: #9b2c2c; }
            table { width: 100%; border-collapse: collapse; }
            th { text-align: left; padding: .5rem .75rem; font-size: .8rem; font-weight: 700;
                 color: #718096; border-bottom: 2px solid #e2e8f0; }
            td { padding: .6rem .75rem; border-bottom: 1px solid #e2e8f0; font-size: .9rem; }
            tr:hover td { background: #f7fafc; }
            a { color: #2b6cb0; }
            .dl { display: grid; grid-template-columns: 180px 1fr; gap: .4rem .75rem; }
            .dl dt { font-weight: 600; color: #4a5568; }
            .dl dd { margin: 0; }
            .pagination { display: flex; gap: .5rem; margin-top: 1rem; }
            .pag-btn { padding: .4rem .9rem; border: 1px solid #cbd5e0; border-radius: 6px;
                       text-decoration: none; color: #2d3748; font-size: .85rem; }
            .pag-btn.active { background: #2b6cb0; color: #fff; border-color: #2b6cb0; }
          </style>
        </head>
        <body>
        <header>
          <h1>Birth Registry</h1>
          <nav>
            <a href="/api/register">Register Birth</a>
            <a href="/api/search">Search Records</a>
            <a href="/api/health">Health</a>
          </nav>
        </header>
        <main>
        {BODY}
        </main>
        </body>
        </html>
        """;

    public static string RegistrationForm(BirthRecordDto? prefill, List<string>? errors)
    {
        string v(string field) => prefill is null ? "" : field switch
        {
            "ChildFirstName"  => prefill.ChildFirstName,
            "ChildLastName"   => prefill.ChildLastName,
            "DateOfBirth"     => prefill.DateOfBirth,
            "Gender"          => prefill.Gender,
            "HospitalName"    => prefill.HospitalName ?? "",
            "CityOfBirth"     => prefill.CityOfBirth ?? "",
            "CountryOfBirth"  => prefill.CountryOfBirth ?? "United Kingdom",
            "BirthWeightKg"   => prefill.BirthWeightKg ?? "",
            "FatherFirstName" => prefill.FatherFirstName,
            "FatherLastName"  => prefill.FatherLastName,
            "MotherFirstName" => prefill.MotherFirstName,
            "MotherMaidenName" => prefill.MotherMaidenName,
            "ParentAddress"   => prefill.ParentAddress,
            "ParentPostcode"  => prefill.ParentPostcode ?? "",
            "ContactPhone"    => prefill.ContactPhone ?? "",
            "ContactEmail"    => prefill.ContactEmail ?? "",
            "Notes"           => prefill.Notes ?? "",
            _ => ""
        };

        string sel(string field, string opt) =>
            v(field) == opt ? " selected" : "";

        string errorsHtml = errors?.Count > 0
            ? $"<div class='alert alert-error'><strong>Please fix the following:</strong><ul>{string.Join("", errors.Select(e => $"<li>{e}</li>"))}</ul></div>"
            : "";

        var body = $"""
            <div class='card'>
              <h2>Register a New Birth</h2>
              {errorsHtml}
              <form method='post' action='/api/register'>
                <p class='section-title'>Child Information</p>
                <div class='grid'>
                  <div>
                    <label for='ChildFirstName'>First Name *</label>
                    <input id='ChildFirstName' name='ChildFirstName' value='{v("ChildFirstName")}' required />
                  </div>
                  <div>
                    <label for='ChildLastName'>Last Name *</label>
                    <input id='ChildLastName' name='ChildLastName' value='{v("ChildLastName")}' required />
                  </div>
                  <div>
                    <label for='DateOfBirth'>Date of Birth *</label>
                    <input type='date' id='DateOfBirth' name='DateOfBirth' value='{v("DateOfBirth")}' required />
                  </div>
                  <div>
                    <label for='Gender'>Gender *</label>
                    <select id='Gender' name='Gender' required>
                      <option value=''>-- Select --</option>
                      <option value='Male'{sel("Gender","Male")}>Male</option>
                      <option value='Female'{sel("Gender","Female")}>Female</option>
                      <option value='Other'{sel("Gender","Other")}>Other</option>
                    </select>
                  </div>
                  <div>
                    <label for='BirthWeightKg'>Birth Weight (kg)</label>
                    <input type='number' step='0.001' id='BirthWeightKg' name='BirthWeightKg' value='{v("BirthWeightKg")}' placeholder='e.g. 3.25' />
                  </div>
                  <div>
                    <label for='HospitalName'>Hospital / Place of Birth</label>
                    <input id='HospitalName' name='HospitalName' value='{v("HospitalName")}' />
                  </div>
                  <div>
                    <label for='CityOfBirth'>City of Birth</label>
                    <input id='CityOfBirth' name='CityOfBirth' value='{v("CityOfBirth")}' />
                  </div>
                  <div>
                    <label for='CountryOfBirth'>Country of Birth</label>
                    <input id='CountryOfBirth' name='CountryOfBirth' value='{v("CountryOfBirth")}' />
                  </div>
                </div>

                <p class='section-title'>Father's Details</p>
                <div class='grid'>
                  <div>
                    <label for='FatherFirstName'>First Name *</label>
                    <input id='FatherFirstName' name='FatherFirstName' value='{v("FatherFirstName")}' required />
                  </div>
                  <div>
                    <label for='FatherLastName'>Last Name *</label>
                    <input id='FatherLastName' name='FatherLastName' value='{v("FatherLastName")}' required />
                  </div>
                </div>

                <p class='section-title'>Mother's Details</p>
                <div class='grid'>
                  <div>
                    <label for='MotherFirstName'>First Name *</label>
                    <input id='MotherFirstName' name='MotherFirstName' value='{v("MotherFirstName")}' required />
                  </div>
                  <div>
                    <label for='MotherMaidenName'>Maiden Name *</label>
                    <input id='MotherMaidenName' name='MotherMaidenName' value='{v("MotherMaidenName")}' required />
                  </div>
                </div>

                <p class='section-title'>Contact & Address</p>
                <div class='grid'>
                  <div class='full'>
                    <label for='ParentAddress'>Address *</label>
                    <input id='ParentAddress' name='ParentAddress' value='{v("ParentAddress")}' required />
                  </div>
                  <div>
                    <label for='ParentPostcode'>Postcode</label>
                    <input id='ParentPostcode' name='ParentPostcode' value='{v("ParentPostcode")}' />
                  </div>
                  <div>
                    <label for='ContactPhone'>Phone</label>
                    <input type='tel' id='ContactPhone' name='ContactPhone' value='{v("ContactPhone")}' />
                  </div>
                  <div class='full'>
                    <label for='ContactEmail'>Email</label>
                    <input type='email' id='ContactEmail' name='ContactEmail' value='{v("ContactEmail")}' />
                  </div>
                </div>

                <p class='section-title'>Additional Notes</p>
                <textarea name='Notes' rows='3' placeholder='Any additional notes...'>{v("Notes")}</textarea>

                <div style='margin-top:1.5rem; display:flex; gap:1rem;'>
                  <button type='submit' class='btn btn-primary'>Register Birth</button>
                  <a href='/api/search' class='btn btn-secondary'>Search Records</a>
                </div>
              </form>
            </div>
            """;

        return Layout.Replace("{TITLE}", "Register Birth").Replace("{BODY}", body);
    }

    public static string RegistrationSuccess(BirthRecord record)
    {
        var body = $"""
            <div class='card'>
              <div class='alert alert-success'>
                <strong>Birth successfully registered!</strong><br />
                Your registration number is: <strong>{record.RegistrationNumber}</strong>
              </div>
              <h2>Registration Confirmation</h2>
              <dl class='dl'>
                <dt>Registration No.</dt><dd><strong>{record.RegistrationNumber}</strong></dd>
                <dt>Child's Full Name</dt><dd>{record.ChildFirstName} {record.ChildLastName}</dd>
                <dt>Date of Birth</dt><dd>{record.DateOfBirth:dd MMMM yyyy}</dd>
                <dt>Gender</dt><dd>{record.Gender}</dd>
                <dt>Place of Birth</dt><dd>{record.HospitalName} {record.CityOfBirth}</dd>
                <dt>Father</dt><dd>{record.FatherFirstName} {record.FatherLastName}</dd>
                <dt>Mother</dt><dd>{record.MotherFirstName} {record.MotherMaidenName}</dd>
                <dt>Status</dt><dd><span class='badge badge-green'>{record.RegistrationStatus}</span></dd>
                <dt>Registered At</dt><dd>{record.RegisteredAt:dd MMM yyyy HH:mm} UTC</dd>
              </dl>
              <div style='margin-top:1.5rem; display:flex; gap:1rem;'>
                <a href='/api/records/{record.Id}' class='btn btn-primary'>View Full Record</a>
                <a href='/api/register' class='btn btn-secondary'>Register Another</a>
                <a href='/api/search' class='btn btn-secondary'>Search Records</a>
              </div>
            </div>
            """;
        return Layout.Replace("{TITLE}", "Registration Confirmed").Replace("{BODY}", body);
    }

    public static string SearchForm(string? lastName, string? from, string? to,
        IEnumerable<BirthRecord>? results, int total, int page = 1)
    {
        string resultsHtml = "";
        if (results is not null)
        {
            var rows = string.Join("", results.Select(r => $"""
                <tr>
                  <td><a href='/api/records/{r.Id}'>{r.RegistrationNumber}</a></td>
                  <td>{r.ChildFirstName} {r.ChildLastName}</td>
                  <td>{r.DateOfBirth:dd MMM yyyy}</td>
                  <td>{r.Gender}</td>
                  <td>{r.HospitalName}</td>
                  <td><span class='badge {BadgeClass(r.RegistrationStatus)}'>{r.RegistrationStatus}</span></td>
                  <td>{r.RegisteredAt:dd MMM yyyy}</td>
                </tr>
                """));

            string noResults = total == 0 ? "<tr><td colspan='7' style='text-align:center;color:#718096;padding:2rem'>No records found.</td></tr>" : "";

            string pagHtml = "";
            if (total > 20)
            {
                int totalPages = (int)Math.Ceiling(total / 20.0);
                pagHtml = "<div class='pagination'>" +
                    string.Join("", Enumerable.Range(1, totalPages).Select(pg =>
                        $"<a class='pag-btn{(pg == page ? " active" : "")}' href='/api/search?lastName={lastName}&from={from}&to={to}&page={pg}'>{pg}</a>")) +
                    "</div>";
            }

            resultsHtml = $"""
                <div class='card' style='margin-top:1rem'>
                  <p style='color:#4a5568; font-size:.9rem'>Showing {results.Count()} of {total} record(s)</p>
                  <table>
                    <thead>
                      <tr>
                        <th>Reg. No.</th><th>Child Name</th><th>Date of Birth</th>
                        <th>Gender</th><th>Hospital</th><th>Status</th><th>Registered</th>
                      </tr>
                    </thead>
                    <tbody>{rows}{noResults}</tbody>
                  </table>
                  {pagHtml}
                </div>
                """;
        }

        var body = $"""
            <div class='card'>
              <h2>Search Birth Records</h2>
              <form method='get' action='/api/search'>
                <div class='grid'>
                  <div>
                    <label for='lastName'>Child's Last Name</label>
                    <input id='lastName' name='lastName' value='{lastName ?? ""}' placeholder='e.g. Smith' />
                  </div>
                  <div></div>
                  <div>
                    <label for='from'>Date of Birth From</label>
                    <input type='date' id='from' name='from' value='{from ?? ""}' />
                  </div>
                  <div>
                    <label for='to'>Date of Birth To</label>
                    <input type='date' id='to' name='to' value='{to ?? ""}' />
                  </div>
                </div>
                <div style='margin-top:1rem; display:flex; gap:1rem;'>
                  <button type='submit' class='btn btn-primary'>Search</button>
                  <a href='/api/register' class='btn btn-secondary'>Register New Birth</a>
                </div>
              </form>
            </div>
            {resultsHtml}
            """;

        return Layout.Replace("{TITLE}", "Search Records").Replace("{BODY}", body);
    }

    public static string RecordDetail(BirthRecord r)
    {
        var body = $"""
            <div class='card'>
              <div style='display:flex; justify-content:space-between; align-items:flex-start;'>
                <h2>Birth Record — {r.RegistrationNumber}</h2>
                <span class='badge {BadgeClass(r.RegistrationStatus)}' style='font-size:.9rem;padding:.3rem .8rem'>{r.RegistrationStatus}</span>
              </div>
              <p class='section-title'>Child</p>
              <dl class='dl'>
                <dt>Full Name</dt><dd>{r.ChildFirstName} {r.ChildLastName}</dd>
                <dt>Date of Birth</dt><dd>{r.DateOfBirth:dd MMMM yyyy}</dd>
                <dt>Gender</dt><dd>{r.Gender}</dd>
                <dt>Birth Weight</dt><dd>{(r.BirthWeightKg.HasValue ? $"{r.BirthWeightKg:F3} kg" : "—")}</dd>
                <dt>Place of Birth</dt><dd>{r.HospitalName}, {r.CityOfBirth}, {r.CountryOfBirth}</dd>
              </dl>
              <p class='section-title'>Parents</p>
              <dl class='dl'>
                <dt>Father</dt><dd>{r.FatherFirstName} {r.FatherLastName}</dd>
                <dt>Mother</dt><dd>{r.MotherFirstName} {r.MotherMaidenName}</dd>
                <dt>Address</dt><dd>{r.ParentAddress}{(r.ParentPostcode != null ? $", {r.ParentPostcode}" : "")}</dd>
                <dt>Phone</dt><dd>{r.ContactPhone ?? "—"}</dd>
                <dt>Email</dt><dd>{r.ContactEmail ?? "—"}</dd>
              </dl>
              <p class='section-title'>Registry Details</p>
              <dl class='dl'>
                <dt>Registration No.</dt><dd><strong>{r.RegistrationNumber}</strong></dd>
                <dt>Registered At</dt><dd>{r.RegisteredAt:dd MMM yyyy HH:mm} UTC</dd>
                <dt>Notes</dt><dd>{r.Notes ?? "—"}</dd>
              </dl>
              <div style='margin-top:1.5rem; display:flex; gap:1rem;'>
                <a href='/api/search' class='btn btn-secondary'>Back to Search</a>
                <a href='/api/register' class='btn btn-secondary'>Register Another</a>
              </div>
            </div>
            """;

        return Layout.Replace("{TITLE}", $"Record {r.RegistrationNumber}").Replace("{BODY}", body);
    }

    public static string ErrorPage(string message)
    {
        var body = $"""
            <div class='card'>
              <div class='alert alert-error'>{message}</div>
              <a href='/api/register' class='btn btn-secondary'>Go to Registration</a>
            </div>
            """;
        return Layout.Replace("{TITLE}", "Error").Replace("{BODY}", body);
    }

    private static string BadgeClass(string status) => status switch
    {
        "Registered" => "badge-green",
        "Verified"   => "badge-blue",
        "Pending"    => "badge-yellow",
        "Rejected"   => "badge-red",
        _ => ""
    };
}
