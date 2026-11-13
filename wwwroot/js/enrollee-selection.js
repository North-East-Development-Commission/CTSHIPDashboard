const deleteForm = document.getElementById("delete-selected-enrollees");
const selectPage = document.getElementById("select-page-enrollees");
const enrolleeCheckboxes = [...deleteForm.querySelectorAll('input[name="ids"]')];
const deleteButton = deleteForm.querySelector('button[type="submit"]');

function updateSelection() {
    const count = enrolleeCheckboxes.filter(checkbox => checkbox.checked).length;
    selectPage.checked = count === enrolleeCheckboxes.length;
    selectPage.indeterminate = count > 0 && count < enrolleeCheckboxes.length;
    deleteButton.disabled = count === 0;
    deleteButton.textContent = count ? `Delete selected (${count})` : "Delete selected";
    return count;
}

selectPage.addEventListener("change", () => {
    for (const checkbox of enrolleeCheckboxes) {
        checkbox.checked = selectPage.checked;
    }
    updateSelection();
});

for (const checkbox of enrolleeCheckboxes) {
    checkbox.addEventListener("change", updateSelection);
}

deleteForm.addEventListener("submit", event => {
    const count = updateSelection();
    if (!count || !window.confirm(`Permanently delete the ${count} selected enrollee record(s)?`)) {
        event.preventDefault();
    }
});

window.addEventListener("pageshow", updateSelection);
updateSelection();
