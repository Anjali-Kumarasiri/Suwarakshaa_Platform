// Content for the Latest updates section (home page cards, /updates list and /updates/:slug pages).
// Block types: p (paragraph), h (heading), ul / ol (lists), ex (weak vs strong example).
export const UPDATES = [
  {
    slug: "new-shelters-gampaha", tag: "Update", date: "12 Nov 2026", img: "/images/news-1.jpg",
    title: "Flood response: new shelters added in Gampaha",
    summary: "Capacity and facilities are now visible to coordinators.",
    body: [
      { t: "p", x: "Several new shelters in the Gampaha area have been registered on Suwarakshaa. Their capacity, facilities and accessibility details are now visible to coordinators, so families can be matched with a place that fits their needs." },
      { t: "h", x: "What coordinators can now see" },
      { t: "ul", items: ["Free spaces and total capacity for each shelter", "Facilities such as drinking water, sanitation and a medical corner", "Wheelchair and elderly access", "Distance from the family's location"] },
      { t: "h", x: "If you need a shelter" },
      { t: "p", x: "Submit a request and choose Shelter, or look at approved shelters on the Shelters map. The system only suggests a match: a coordinator confirms every allocation." },
    ],
  },
  {
    slug: "volunteer-orientation", tag: "Event", date: "13 Nov 2026", img: "/images/news-2.jpg",
    title: "Volunteer orientation this Saturday",
    summary: "Learn how requests are matched to people nearby.",
    body: [
      { t: "p", x: "New and existing volunteers are invited to this Saturday's orientation. It explains how requests reach volunteers and what is expected on the ground." },
      { t: "h", x: "What you will learn" },
      { t: "ul", items: ["How requests are sorted by urgency", "How tasks are matched to volunteers nearby", "Safety basics, and when to stop and ask for help", "How your volunteer hours are recorded"] },
      { t: "h", x: "How to join" },
      { t: "p", x: "Sign in and register as a volunteer. The venue and time will be sent to registered volunteers as a notification." },
    ],
  },
  {
    slug: "how-to-write-a-request", tag: "Guide", date: "14 Nov 2026", img: "/images/news-3.jpg", cta: ["Request help now", "/request"],
    title: "How to write a request that gets help faster",
    summary: "Say what you need, how many people, and where.",
    body: [
      { t: "p", x: "Coordinators read many requests at once. A clear request can be sorted, matched and answered sooner. It only takes a minute." },
      { t: "p", x: "If anyone is in immediate danger, call your local emergency number first. Then send a request here." },
      { t: "h", x: "1. Choose the right category" },
      { t: "p", x: "Pick what you need most: Food, Medicine, Water, Shelter, Transport, Accessibility or Other. You can mention other needs in the description." },
      { t: "h", x: "2. Describe the situation in two or three sentences" },
      { t: "ul", items: ["What is happening (for example, water inside the house)", "What exactly you need (name of medicine, number of meals)", "Anyone with special needs: babies, elderly people, pregnant women, people with disabilities"] },
      { t: "h", x: "3. Say how many people are affected" },
      { t: "p", x: "Count everyone who needs the help, including children." },
      { t: "h", x: "4. Tell us where you are" },
      { t: "p", x: "Tap Use my location, or type an address with a landmark (a temple, school or bus stand). Without a location, help cannot reach you." },
      { t: "h", x: "5. Set the urgency honestly" },
      { t: "p", x: "1 is low and 5 is life-threatening. If you are not sure, leave it as Not sure and describe the situation. A person reviews every request." },
      { t: "h", x: "A weak request and a strong request" },
      { t: "ex", weak: "Need help. Please come.", strong: "Family of 5 including a 2-year-old and an elderly man who needs insulin. Water has reached the first floor. We need insulin, drinking water and a safe place to stay. Near Gampaha bus stand." },
      { t: "h", x: "What happens next" },
      { t: "ol", items: ["The system suggests a category and urgency (it only suggests).", "A coordinator reviews it and approves, changes, rejects or defers it.", "You are notified when the status changes. Track it any time under My requests."] },
      { t: "h", x: "Please avoid" },
      { t: "ul", items: ["Sending the same request many times. Duplicates are flagged and slow things down.", "Sharing passwords or bank details in the description."] },
    ],
  },
  {
    slug: "track-your-request", tag: "Guide", date: "11 Nov 2026", img: "/images/shelter.jpg", cta: ["Go to My requests", "/request"],
    title: "How to track your request",
    summary: "What each status means and where to see updates.",
    body: [
      { t: "p", x: "After you submit a request you can follow it from start to finish." },
      { t: "h", x: "Where to look" },
      { t: "p", x: "Log in and open Request help. Under My requests every request shows its reference number and current status." },
      { t: "h", x: "What the statuses mean" },
      { t: "ul", items: ["Submitted: we received your request", "Under review / Verified: a person is checking it", "Escalated: it needs urgent attention", "Assigned / In progress: a team or volunteer is working on it", "Fulfilled / Closed: help was delivered and the case is finished", "Rejected / Merged: it was declined, or combined with a duplicate request"] },
      { t: "h", x: "Notifications" },
      { t: "p", x: "When the status changes you get a pop-up and an entry under the bell icon." },
    ],
  },
  {
    slug: "urgency-levels", tag: "Guide", date: "10 Nov 2026", img: "/images/flood.jpg", cta: ["Request help", "/request"],
    title: "What the urgency levels (1 to 5) mean",
    summary: "Choose the right urgency so help is sorted fairly.",
    body: [
      { t: "p", x: "When you submit a request you can set an urgency from 1 to 5. It helps with sorting, but a coordinator always makes the final decision." },
      { t: "ul", items: ["5: life-threatening (trapped, serious injury, urgent medicine such as insulin)", "4: very urgent (small children, elderly or pregnant people at risk)", "3: medium (help needed today)", "2: can wait a little", "1: low (no immediate risk)"] },
      { t: "p", x: "If you are not sure, choose Not sure and describe the situation. For immediate danger, call your local emergency number first." },
    ],
  },
  {
    slug: "how-to-donate", tag: "Guide", date: "9 Nov 2026", img: "/images/volunteers.jpg", cta: ["Donate", "/donate"],
    title: "How to donate and see where it goes",
    summary: "Four steps, and every campaign shows how funds are used.",
    body: [
      { t: "ol", items: ["Open Donate and choose a campaign.", "Pick an amount or type your own, and choose LKR or USD.", "Tick Donate anonymously if you prefer.", "Log in (or create an account) and confirm."] },
      { t: "p", x: "After you donate you get a pop-up and a notification. Next to each campaign you can see how much was raised, how much was spent and what is left." },
      { t: "p", x: "This is a sandbox: no real payment is taken." },
    ],
  },
  {
    slug: "assistant-languages", tag: "Update", date: "11 Nov 2026", img: "/images/hero.jpg",
    title: "The assistant now answers in Sinhala and Tamil",
    summary: "Chat in English, සිංහල or தமிழ்.",
    body: [
      { t: "p", x: "The chat button at the bottom right of every page can now answer in English, Sinhala or Tamil. Switch language with EN, සිං or தமிழ்." },
      { t: "p", x: "It explains how to use the portal and gives basic safety guidance. It cannot approve help or give medical advice." },
    ],
  },
  {
    slug: "donation-transparency", tag: "Update", date: "10 Nov 2026", img: "/images/volunteers.jpg", cta: ["See campaigns", "/donate"],
    title: "See how every donation is used",
    summary: "Raised, spent and remaining, for each campaign.",
    body: [
      { t: "p", x: "Each campaign now shows how much was raised, how much has been spent and what is left, with the purpose of each spending entry." },
      { t: "p", x: "Suspicious donations are flagged for a person to review. They are never rejected automatically." },
    ],
  },
  {
    slug: "popup-notifications", tag: "Update", date: "9 Nov 2026", img: "/images/hero.jpg",
    title: "Pop-up notifications for requests and donations",
    summary: "Know straight away when something changes.",
    body: [
      { t: "p", x: "When you submit a request or make a donation you now get a pop-up. When a request changes status, a pop-up appears and the bell icon shows a count." },
      { t: "p", x: "Click the bell to read your recent notifications." },
    ],
  },
  {
    slug: "shelter-staff-briefing", tag: "Event", date: "8 Nov 2026", img: "/images/shelter.jpg",
    title: "Briefing for shelter coordinators",
    summary: "Keeping capacity up to date and handling allocations.",
    body: [
      { t: "p", x: "A short briefing for shelter coordinators on how to register a shelter, keep its capacity accurate and handle allocation requests safely." },
      { t: "ul", items: ["Registering facilities and accessibility", "Updating free spaces", "Checking out families when they leave"] },
      { t: "p", x: "Staff accounts receive the joining details as a notification." },
    ],
  },
];
// the three cards shown on the Home page (one Update, one Event, one Guide)
export const HOME_SLUGS = ["new-shelters-gampaha", "volunteer-orientation", "how-to-write-a-request"];
export const getUpdate = (slug) => UPDATES.find((u) => u.slug === slug);
