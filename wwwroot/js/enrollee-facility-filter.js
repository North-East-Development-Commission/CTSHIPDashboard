const filters = document.getElementById("enrollee-filters");
const state = filters.elements.namedItem("state");
const hmo = filters.elements.namedItem("hmo");
const facility = filters.elements.namedItem("facility");
const status = document.getElementById("facility-filter-status");
let pendingRequest;
let selectedFacility = facility.value;

facility.addEventListener("change", () => {
    selectedFacility = facility.value;
});

async function refreshFacilities() {
    pendingRequest?.abort();
    const request = new AbortController();
    pendingRequest = request;

    const url = new URL(filters.dataset.facilitiesUrl, window.location.href);
    url.searchParams.set("state", state.value);
    url.searchParams.set("hmo", hmo.value);
    facility.disabled = true;
    facility.replaceChildren(new Option("All Facilities", ""));
    status.textContent = "Loading facilities…";

    try {
        const response = await fetch(url, { signal: request.signal });
        if (!response.ok) {
            throw new Error("Facility lookup failed");
        }

        const options = await response.json();
        if (pendingRequest !== request) {
            return;
        }

        for (const option of options) {
            facility.add(new Option(option.name, option.id));
        }
        facility.value = options.some(option => String(option.id) === selectedFacility) ? selectedFacility : "";
        selectedFacility = facility.value;
        facility.disabled = false;
        status.textContent = options.length ? "" : "No facilities match the selected filters.";
    } catch {
        if (pendingRequest === request) {
            status.textContent = "Could not load facilities. Apply the filters to reload the list.";
        }
    }
}

state.addEventListener("change", refreshFacilities);
hmo.addEventListener("change", refreshFacilities);
