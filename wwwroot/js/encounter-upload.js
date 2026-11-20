const form = document.getElementById("encounter-upload");
const hmo = form.elements.namedItem("hmoId");
const facility = form.elements.namedItem("providerId");
const status = document.getElementById("encounter-facility-status");
const options = [...document.getElementById("encounter-facility-options").content.querySelectorAll("option")];
const submit = form.querySelector('button[type="submit"]');

function updateFacilities(preferredFacility) {
    const matches = options.filter(option => option.dataset.hmo === hmo.value);
    facility.replaceChildren(new Option("Select a facility", ""), ...matches.map(option => option.cloneNode(true)));
    facility.value = matches.some(option => option.value === preferredFacility) ? preferredFacility : "";
    facility.disabled = matches.length === 0;
    submit.disabled = !facility.value;
    status.textContent = !hmo.value ? "Select an HMO first."
        : matches.length === 0 ? "This HMO has no active facilities." : "";
}

hmo.addEventListener("change", () => updateFacilities(""));
facility.addEventListener("change", () => { submit.disabled = !facility.value; });
window.addEventListener("pageshow", () => updateFacilities(facility.value));
updateFacilities(facility.value);
