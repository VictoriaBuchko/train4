const data = @Html.Raw(Json.Serialize(ViewBag.SeatsData ?? new List < object > ()));
let selectedSeat = null;

const carriages = {};
data.forEach(seat => {
    const num = seat.carriage_number;
    if (!carriages[num]) carriages[num] = { number: num, type: seat.carriage_type, seats: [] };
    carriages[num].seats.push(seat);
});

function renderTabs() {
    const tabs = Object.keys(carriages).sort((a, b) => a - b).map(num =>
        `<button class="btn btn-outline-primary me-2" onclick="showCarriage(${num})">
                Вагон ${num} (${carriages[num].type})
            </button>`
    ).join('');
    document.getElementById('carriageTabs').innerHTML = tabs;
}

function showCarriage(num) {
    const c = carriages[num];
    const available = c.seats.filter(s => s.status === 'available').length;

    document.getElementById('carriageContent').innerHTML = `
            <div class="border rounded p-3">
                <h4>${c.type} - Вагон №${c.number}</h4>
                <p>Вільних: ${available} з ${c.seats.length}</p>
                <div class="seats-container">
                    ${c.seats.map(seat =>
        `<div class="seat ${seat.status}" 
                              data-id="${seat.seat_id}" 
                              data-number="${seat.seat_number}"
                              data-carriage="${seat.carriage_number}"
                              onclick="selectSeat(this)">${seat.seat_number}</div>`
    ).join('')}
                </div>
            </div>`;

    document.querySelectorAll('#carriageTabs button').forEach(b => b.classList.remove('btn-primary'));
    document.querySelectorAll('#carriageTabs button').forEach(b => b.classList.add('btn-outline-primary'));
    event?.target?.classList?.remove('btn-outline-primary');
    event?.target?.classList?.add('btn-primary');
}

async function selectSeat(el) {
    if (!el.classList.contains('available')) return;

    document.querySelectorAll('.seat.selected').forEach(s => s.classList.remove('selected'));
    el.classList.add('selected');

    selectedSeat = {
        id: el.dataset.id,
        number: el.dataset.number,
        carriage: el.dataset.carriage
    };

    const trainId = @ViewBag.TrainId;
    const travelDate = '@ViewBag.TravelDate';
    const fromStationId = @ViewBag.FromStationId;
    const toStationId = @ViewBag.ToStationId;

    try {
        const response = await fetch(`/Home/GetTicketDetails?trainId=${trainId}&travelDate=${travelDate}&seatId=${selectedSeat.id}&fromStationId=${fromStationId}&toStationId=${toStationId}`);
        const data = await response.json();

        document.getElementById("modalFrom").textContent = data.from_station_name;
        document.getElementById("modalTo").textContent = data.to_station_name;
        document.getElementById("modalCarriage").textContent = data.carriage_number;
        document.getElementById("modalSeat").textContent = data.seat_number;
        document.getElementById("modalDuration").textContent = `${data.departure_time} — ${data.arrival_time}`;
        document.getElementById("modalPrice").textContent = data.total_price;

        document.getElementById("ticketModal").style.display = "block";
    } catch (error) {
        alert("Помилка при отриманні інформації про квиток.");
    }
}

function closeTicketModal() {
    document.getElementById("ticketModal").style.display = "none";
}

async function confirmBooking() {
    if (!selectedSeat) return;

    const confirmed = confirm("Вы действительно хотите забронировать этот билет?");
    if (!confirmed) return;

    try {
        const response = await fetch('/Home/BookSeatConfirmed', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': document.querySelector('[name="__RequestVerificationToken"]').value
            },
            body: new URLSearchParams({
                seatId: selectedSeat.id,
                trainId: @ViewBag.TrainId,
                travelDate: '@ViewBag.TravelDate',
                fromStationId: '@ViewBag.FromStationId',
                toStationId: '@ViewBag.ToStationId'
            })
        });

        const result = await response.json();

        if (result.success) {
            showSuccessModal(result);
            closeTicketModal();
            setTimeout(() => {
                location.reload(); // Обновляем страницу через 3 секунды
            }, 3000);
        } else {
            showErrorModal(result.message);
        }
    } catch (error) {
        showErrorModal('Ошибка при бронировании билета.');
    }
}

// Функция для показа успешного бронирования
function showSuccessModal(result) {
    const modal = document.createElement('div');
    modal.innerHTML = `
        <div style="position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.7); z-index: 9999; display: flex; justify-content: center; align-items: center;">
            <div style="background: white; padding: 30px; border-radius: 15px; box-shadow: 0 10px 30px rgba(0,0,0,0.3); max-width: 500px; text-align: center; animation: modalSlideIn 0.3s ease-out;">
                <div style="color: #28a745; font-size: 48px; margin-bottom: 20px;">
                    ✅
                </div>
                <h3 style="color: #28a745; margin-bottom: 20px;">Билет успешно забронирован!</h3>
                
                <div style="background: #f8f9fa; padding: 20px; border-radius: 10px; margin: 20px 0; text-align: left;">
                    <div style="display: flex; justify-content: space-between; margin-bottom: 10px;">
                        <span style="font-weight: 600;">Номер билета:</span>
                        <span style="color: #007bff; font-weight: 700;">#${result.ticket_id}</span>
                    </div>
                    <div style="display: flex; justify-content: space-between; margin-bottom: 10px;">
                        <span style="font-weight: 600;">Стоимость:</span>
                        <span style="color: #28a745; font-weight: 700; font-size: 18px;">${result.total_price} грн</span>
                    </div>
                    <div style="display: flex; justify-content: space-between; margin-bottom: 10px;">
                        <span style="font-weight: 600;">Место:</span>
                        <span>${selectedSeat.number} (вагон ${selectedSeat.carriage})</span>
                    </div>
                    <div style="display: flex; justify-content: space-between;">
                        <span style="font-weight: 600;">Дата поездки:</span>
                        <span>@ViewBag.TravelDate</span>
                    </div>
                </div>

                <div style="background: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 8px; margin: 20px 0;">
                    <p style="margin: 0; color: #856404;">
                        <strong>💡 Важно:</strong> Для оплаты билета обратитесь к бухгалтеру.
                    </p>
                </div>

                <button onclick="this.parentElement.parentElement.remove()" 
                        style="background: #007bff; color: white; border: none; padding: 12px 30px; border-radius: 8px; font-size: 16px; cursor: pointer; margin-top: 10px;">
                    Понятно
                </button>

                <p style="color: #6c757d; font-size: 14px; margin-top: 15px;">
                    Страница обновится через несколько секунд...
                </p>
            </div>
        </div>
    `;


    document.body.appendChild(modal);
}

// Функция для показа ошибки
function showErrorModal(message) {
    const modal = document.createElement('div');
    modal.innerHTML = `
        <div style="position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.7); z-index: 9999; display: flex; justify-content: center; align-items: center;">
            <div style="background: white; padding: 30px; border-radius: 15px; box-shadow: 0 10px 30px rgba(0,0,0,0.3); max-width: 400px; text-align: center;">
                <div style="color: #dc3545; font-size: 48px; margin-bottom: 20px;">
                    ❌
                </div>
                <h3 style="color: #dc3545; margin-bottom: 20px;">Ошибка бронирования</h3>
                <p style="color: #6c757d; margin-bottom: 30px;">${message}</p>
                <button onclick="this.parentElement.parentElement.remove()" 
                        style="background: #dc3545; color: white; border: none; padding: 12px 30px; border-radius: 8px; font-size: 16px; cursor: pointer;">
                    Закрыть
                </button>
            </div>
        </div>
    `;

    document.body.appendChild(modal);
}

if (Object.keys(carriages).length > 0) {
    renderTabs();
    showCarriage(Math.min(...Object.keys(carriages)));
}