using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Middleware.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class JwtController : ControllerBase
    {
        // GET: api/<JwtController>
        [AllowAnonymous]
        [HttpGet]
        public string Get([FromBody] string username, [FromBody] string password)
        {
            return "jwt";
        }

        // GET api/<JwtController>/5
        [HttpGet("{id}")]
        public async Task<string> GetAsync()
        {
            return "";//await tokenGenerator.CreateJWT();
        }

        // POST api/<JwtController>
        [HttpPost]
        public void Post([FromBody] string value)
        {

        }

        // PUT api/<JwtController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {

        }

        // DELETE api/<JwtController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {

        }
    }
}
