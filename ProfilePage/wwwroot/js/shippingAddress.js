/* =============================================================
   SHIPPING ADDRESS - PROFESSIONAL MONOCHROME LOGIC
   ============================================================= */

document.addEventListener('DOMContentLoaded', async function () {
    // 1. Element Selectors
    const regionInput = document.getElementById('regionInput');
    const regionList = document.getElementById('regionList');
    const provinceInput = document.getElementById('provinceInput');
    const provinceList = document.getElementById('provinceList');
    const cityInput = document.getElementById('cityInput');
    const cityList = document.getElementById('cityList');
    const barangayInput = document.getElementById('barangayInput');
    const barangayList = document.getElementById('barangayList');

    const houseInput = document.getElementById('houseInput');
    const buildingInput = document.getElementById('buildingInput');
    const streetInput = document.getElementById('streetNameInput');
    const postalInput = document.getElementById('postalInput');
    const defaultToggle = document.getElementById('defaultToggle');

    const addressList = document.getElementById('addressList');
    const emptyState = document.getElementById('emptyState');
    const showFormBtn = document.getElementById('showFormBtn');
    const emptyAddBtn = document.getElementById('emptyAddBtn');
    const newAddressForm = document.getElementById('newAddressForm');
    const saveBtn = document.getElementById('saveBtn');
    const cancelBtn = document.getElementById('cancelBtn'); // Back link
    const formCancelBtn = document.getElementById('formCancelBtn'); // Bottom button

    let addressData = {};
    let editingCard = null;

    // Monochrome Swal Config
    const swalConfig = {
        confirmButtonColor: '#000000',
        iconColor: '#000000',
        background: '#ffffff',
        color: '#000000',
        customClass: {
            confirmButton: 'btn-pill btn-black',
            cancelButton: 'btn-pill btn-gray'
        }
    };

    const regionNames = {
        "01": "Region I (Ilocos Region)", "02": "Region II (Cagayan Valley)",
        "03": "Region III (Central Luzon)", "4A": "Region IV-A (CALABARZON)",
        "4B": "Region IV-B (MIMAROPA)", "05": "Region V (Bicol Region)",
        "06": "Region VI (Western Visayas)", "07": "Region VII (Central Visayas)",
        "08": "Region VIII (Eastern Visayas)", "09": "Region IX (Zamboanga Peninsula)",
        "10": "Region X (Northern Mindanao)", "11": "Region XI (Davao Region)",
        "12": "Region XII (SOCCSKSARGEN)", "13": "Region XIII (Caraga)",
        "ARMM": "ARMM", "CAR": "CAR", "NCR": "NCR (Metro Manila)", "NIR": "NIR (Negros Island Region)"
    };

    // --- 2. Load Data ---
    async function loadData() {
        try {
            // Ensure the path to your JSON is correct based on your root folder
            const response = await fetch('/philippine_provinces_cities_municipalities_and_barangays_2016.json');
            if (!response.ok) throw new Error("Geographic data file not found");
            addressData = await response.json();
            populateDatalist(regionList, Object.keys(addressData), regionNames);
        } catch (e) {
            console.error("Error loading geographic data:", e);
        }
    }

    // --- 3. Cascading Datalist Logic ---
    regionInput.addEventListener('input', function () {
        const code = getCodeByValue(this.value, regionNames);
        provinceInput.value = ''; cityInput.value = ''; barangayInput.value = '';
        if (addressData[code]) {
            populateDatalist(provinceList, Object.keys(addressData[code].province_list || {}));
        }
    });

    provinceInput.addEventListener('input', function () {
        const regCode = getCodeByValue(regionInput.value, regionNames);
        cityInput.value = ''; barangayInput.value = '';
        if (addressData[regCode]?.province_list[this.value]) {
            populateDatalist(cityList, Object.keys(addressData[regCode].province_list[this.value].municipality_list || {}));
        }
    });

    cityInput.addEventListener('input', function () {
        const regCode = getCodeByValue(regionInput.value, regionNames);
        const provName = provinceInput.value;
        barangayInput.value = '';
        if (addressData[regCode]?.province_list[provName]?.municipality_list[this.value]) {
            const brgys = addressData[regCode].province_list[provName].municipality_list[this.value].barangay_list || [];
            populateDatalist(barangayList, brgys);
        }
    });

    // --- 4. Helpers ---
    function populateDatalist(listElement, items, mapping = null) {
        listElement.innerHTML = '';
        items.sort().forEach(item => {
            const option = document.createElement('option');
            option.value = mapping ? mapping[item] : item;
            listElement.appendChild(option);
        });
    }

    function getCodeByValue(val, mapping) {
        return Object.keys(mapping).find(key => mapping[key] === val) || val;
    }

    function checkEmptyState() {
        const cards = addressList.querySelectorAll('.saved-address-card');
        const isEmpty = cards.length === 0;

        emptyState.style.display = isEmpty ? 'block' : 'none';
        addressList.style.display = isEmpty ? 'none' : 'grid';
        showFormBtn.style.display = isEmpty ? 'none' : 'block';
    }

    function resetForm() {
        newAddressForm.querySelectorAll('input').forEach(i => {
            i.value = '';
            i.style.borderColor = '#e0e0e0';
        });
        defaultToggle.checked = false;
        editingCard = null;
    }

    function openForm() {
        newAddressForm.style.display = 'block';
        showFormBtn.style.display = 'none';
        emptyState.style.display = 'none';
        newAddressForm.scrollIntoView({ behavior: 'smooth' });
    }

    function closeForm() {
        newAddressForm.style.display = 'none';
        resetForm();
        checkEmptyState();
    }

    // --- 5. Event Listeners for Toggles ---
    if (showFormBtn) showFormBtn.addEventListener('click', openForm);
    if (emptyAddBtn) emptyAddBtn.addEventListener('click', openForm);
    if (cancelBtn) cancelBtn.addEventListener('click', closeForm);
    if (formCancelBtn) formCancelBtn.addEventListener('click', closeForm);

    // --- 6. Save Logic ---
    saveBtn.addEventListener('click', function (e) {
        e.preventDefault(); // Prevent any default form submission

        const required = [regionInput, provinceInput, cityInput, barangayInput, houseInput, streetInput];
        let isValid = true;

        required.forEach(input => {
            if (!input.value.trim()) {
                input.style.borderColor = '#000000';
                isValid = false;
            } else {
                input.style.borderColor = '#e0e0e0';
            }
        });

        if (!isValid) {
            Swal.fire({
                ...swalConfig,
                title: 'REQUIRED FIELDS',
                text: 'Please complete all highlighted fields.',
                icon: 'warning'
            });
            return;
        }

        const isDefault = defaultToggle.checked;

        // Ensure exclusivity of default address
        if (isDefault) {
            addressList.querySelectorAll('.saved-address-card').forEach(card => {
                card.classList.remove('default-card');
                card.querySelector('.default-badge')?.remove();
            });
        }

        if (editingCard) editingCard.remove();

        const newCard = document.createElement('div');
        newCard.className = `saved-address-card ${isDefault ? 'default-card' : ''}`;

        // Store data for future editing
        Object.assign(newCard.dataset, {
            region: regionInput.value,
            province: provinceInput.value,
            city: cityInput.value,
            barangay: barangayInput.value,
            house: houseInput.value,
            building: buildingInput.value,
            street: streetInput.value,
            postal: postalInput.value
        });

        const fullStreet = `${houseInput.value}${buildingInput.value ? ', ' + buildingInput.value : ''}, ${streetInput.value}`;
        const areaInfo = `${barangayInput.value}, ${cityInput.value}, ${provinceInput.value}, ${regionInput.value} ${postalInput.value}`;

        newCard.innerHTML = `
            ${isDefault ? '<div class="default-badge">DEFAULT</div>' : ''}
            <div class="address-details">
                <p class="street-line"><strong>${fullStreet}</strong></p>
                <p class="area-line">${areaInfo}</p>
            </div>
            <div class="address-controls">
                <button class="control-btn edit-btn" title="Edit"><i class="bi bi-pencil"></i></button>
                <button class="control-btn delete-btn" title="Delete"><i class="bi bi-trash"></i></button>
            </div>
        `;

        isDefault ? addressList.prepend(newCard) : addressList.appendChild(newCard);

        Swal.fire({
            ...swalConfig,
            title: editingCard ? 'UPDATED' : 'SUCCESS',
            text: 'Shipping address saved successfully.',
            icon: 'success',
            timer: 1800,
            showConfirmButton: false
        });

        closeForm();
    });

    // --- 7. Edit & Delete Actions ---
    addressList.addEventListener('click', async (e) => {
        const card = e.target.closest('.saved-address-card');
        if (!card) return;

        // Delete Logic
        if (e.target.closest('.delete-btn')) {
            const result = await Swal.fire({
                ...swalConfig,
                title: 'REMOVE ADDRESS?',
                text: "This will permanently delete this shipping location.",
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'CONFIRM DELETE',
                cancelButtonText: 'CANCEL'
            });

            if (result.isConfirmed) {
                card.remove();
                checkEmptyState();
                Swal.fire({ ...swalConfig, title: 'REMOVED', icon: 'success', timer: 1200, showConfirmButton: false });
            }
            return;
        }

        // Edit Logic
        if (e.target.closest('.edit-btn')) {
            editingCard = card;
            regionInput.value = card.dataset.region;

            // Manually trigger cascading updates to populate datalists before setting values
            regionInput.dispatchEvent(new Event('input'));
            provinceInput.value = card.dataset.province;

            provinceInput.dispatchEvent(new Event('input'));
            cityInput.value = card.dataset.city;

            cityInput.dispatchEvent(new Event('input'));
            barangayInput.value = card.dataset.barangay;

            houseInput.value = card.dataset.house;
            buildingInput.value = card.dataset.building;
            streetInput.value = card.dataset.street;
            postalInput.value = card.dataset.postal;
            defaultToggle.checked = card.classList.contains('default-card');

            openForm();
        }
    });

    // Initialize
    loadData();
    checkEmptyState();
});