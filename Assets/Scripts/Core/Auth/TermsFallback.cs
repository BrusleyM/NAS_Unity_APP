using NAS.Core.Auth.Dtos;

namespace NAS.Core.Auth
{
    /// <summary>
    /// The terms and privacy notice bundled with the app, used when the server's copy cannot be loaded (an older API, no
    /// signal). Keep Version and the text in step with the API's TermsOfUse; if they differ, a server that requires the
    /// terms refuses the registration and asks for the latest version.
    /// </summary>
    public static class TermsFallback
    {
        public const string Version = "2026-10-23";

        public static TermsResponse Create() => new TermsResponse
        {
            version = Version,
            title = "Terms of use and privacy notice",
            sections = new[]
            {
                new TermsSection { heading = "What this app does", body = "NAS Showroom lets you look at cars from a dealership in augmented reality, choose options, see an estimated monthly payment and send an enquiry to that dealership." },
                new TermsSection { heading = "What we collect", body = "Your name, email address and cell number. The cars and options you look at or save. The figures you enter in the estimator (deposit, trade-in value, term, interest rate). How you use the app: which screens you open, how long you spend in AR and how often you change options. We do not ask for your ID number or ID document, photos of you, fingerprints or any other biometric data, bank details or a credit check." },
                new TermsSection { heading = "Why we use it", body = "To run your account, to show you cars and estimates, and to send your enquiry to the dealership you choose. The dealership also gets a score, worked out from how you use the app, that helps its team decide which enquiries to answer first. We also use it to keep the service secure." },
                new TermsSection { heading = "Who sees it", body = "The dealership you send an enquiry to, and its staff. The companies that run the service for us (hosting and email). We do not sell your information and we do not share it with other dealerships." },
                new TermsSection { heading = "Estimates only", body = "The monthly payment shown is an estimate to help you plan. It is not an offer, a quote you can rely on or a credit approval. The dealership confirms the real price and rate." },
                new TermsSection { heading = "Your rights", body = "You may ask to see the information we hold about you, to correct it, or to have it deleted, and you may object to how it is used. Email support@neoxr.co.za. You may also complain to the Information Regulator of South Africa." },
                new TermsSection { heading = "Keeping it safe", body = "Your password is stored scrambled and cannot be read back. The app talks to our servers over an encrypted connection." },
                new TermsSection { heading = "Accepting", body = "By ticking the box you confirm you have read this notice, you are 18 or older, and you agree to your information being used in this way." }
            }
        };
    }
}
