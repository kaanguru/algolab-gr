//***GRID STRATEJISI - ALGOLAB***//
//stratejinin çalıştığı anki fiyatın verdiğiniz gridSteps kademe sayısı altına gridStepCount sayı kadar alış emirleri yazar//
//alış emri gerçekleştiğinde takeProfitSteps kademe sayısı kadar üste satış emri yazar//
// Değiştirebileceğiniz değişkenler aşağıda tanımlanmıştır.//
public string Symbol = "ALTINS1"; // İşlem yapılacak Sembol
public string Symbol2 = "ALTIN"; // İşlem yapılacak Sembol
public int Periyot=1; //fiyat bar sayısının hangi zaman dilimine göre oluşacağını bildirir
public int gridStepCount = 10; // Maksimum kaç adet grid alış emri girilsin (Örneğin 5 verirseniniz toplam 5 alım emri girilir)
public int gridSteps = 2; // Kaç kademede bir alış emri girilsin (Örneğin 5 verirseniz her alım emirlerinin arasında 5 kademe fark olur)
public int takeProfitSteps = 3; // Kaç kademe yukarıya kar al satış emri girilsin (Örneğin 5 verirseniz her gerçekleşen alım emrinin 5 kademe üzerine satış emri yazılır)
public double Amount = 3310; // Her bir Grid alım emiri kaç TL tutarında gönderilsin (Her gönderilen emir burada belirtilen TL cinsinden tutar kadar iletilir)
public double MaxAmount = 33102; // Alınan lotların toplam tutarı maksimum ne kadar olsun (Maksimum elinizde bulunmasını istediğiniz tutar)
public int recalculateMinute = 15; // Kaç dakika sonra emirler silinip yeniden grid hesaplansın. ///---- Eğer 0 olarak girilirse emir silmez ve her gün yeniden başlatır ----/// (Örneğin 60 girereseniz her 60 dakikada bir emirleriniz silinir ve tekrar aktif fiyattan dizilir.)
public bool autoSell = false; // True olarak girilirse elinde biriken lotları kara geçince otomatik satar ///---- False girilirse MaxAmount'a ulaşana kadar lot biriktirir.----/// (Elinizdeki biriken lotların maliyetleri hesaplanır ve tek emir olarak otomatik satılır.)
public double yuzdesatıs=0.005;
// Değiştirilmeyecek değişkenler
public double CurrentPrice; // Anlık fiyatı tutmak için
public double recalculatePrice; //recalculateMinute 0'dan büyük ise son hesaplama fiyatını yazmaktadır.
public Dictionary<string, (double Price, int Quantity, string SellOrderID)> buyOrders = new Dictionary<string, (double, int, string)>(); // Alış emirlerini takip etmek için
public Dictionary<string, (double Price, int Quantity)> sellOrders = new Dictionary<string, (double, int)>(); // Satış emirlerini takip etmek için
public List<(double Price, int Quantity, string SellOrderID)> filledPositions = new List<(double, int, string)>(); // Gerçekleşen Alış emirlerini takip etmek için
public bool firstorder = true; // İlk emir gönderildi mi kontrolü
public DateTime Tarih = DateTime.Now.AddMinutes(-1); // Başlangıçta geçmiş bir zamana ayarlıyoruz
public bool isCancellingOrders = false; // Emirler silinirken true olacak
public int orderdelaysecond = 6; // Kaç saniye sonra emirler gönderilsin
public List<Dictionary<string, object>> delayedOrders = new List<Dictionary<string, object>>(); // Gecikmeli emirleri tutan liste
public string AutosellOrderID = ""; // Otomatik satış emrini takip etmek için
//*********Algolab Fonksiyonları*************//
//Hazır Algolab fonksiyonlarının çağırıldığı alan//
public void Load()
{
    try
    {
        SubscribePrice(Symbol);
        // Tarih değişkenini ilk başta güncelliyoruz
        if (recalculateMinute == 0)
        {
            DateTime dtnext = DateTime.Now.AddDays(1);
            Tarih = new DateTime(dtnext.Year, dtnext.Month, dtnext.Day);
        }
        else
        {
            //Eğer recalculateMinute değerini 0 dan farklı girildi ise o sürede sonra çalışabilmesi için ayarlanıyor**//
            Tarih = DateTime.Now.AddMinutes(recalculateMinute);
        }
    }
    catch (Exception ex)
    {
         SendMessage(MessageTypes.Log, $"Exception in Load: {ex.Message}");
    }
}
// Fiyat değiştiğinde çağrılan fonksiyon
public void PriceChanged(Tick t)
{
    try
    {
        if (t != null && t.Price != 0)
        {
             CurrentPrice = t.Price; // Anlık fiyatı güncelliyoruz
             // Gecikmeli emirleri kontrol edip zamanı gelenleri gönderiyoruz
             ExecuteDelayedOrders();
             double averageCost = GetAverageCost();
             double takeProfitPrice = CalculateSellPrice(averageCost, takeProfitSteps);
             int availableQuantity = GetAvailableQuantity();
             
             // SendMessage(MessageTypes.Log, "Elde kalan lotların Ortalama maliyeti: " + averageCost.ToString("F2") + " ::Elde Kalan Lot miktarı: " + availableQuantity.ToString());
     
             // Eğer AutosellOrderID boşsa ve koşullar sağlanıyorsa otomatik satış emri gönder
             if (autoSell == true && 
                 string.IsNullOrEmpty(AutosellOrderID) && 
                 CurrentPrice >= (takeProfitPrice*(1+yuzdesatıs)) && 
                 availableQuantity > 0 && 
                 CurrentPrice > 0)
             {
                AutosellOrderID = SendOrder(Symbol2, Directions.SELL, availableQuantity, PriceTypes.Limit, Math.Round(CurrentPrice,2));
     
                 if (!string.IsNullOrEmpty(AutosellOrderID))
                 {
                     sellOrders.Add(AutosellOrderID, (CurrentPrice, availableQuantity));
                     // SendMessage(MessageTypes.Log, $"Auto Limit sell order sent at Price= {CurrentPrice}: Quantity={availableQuantity}: Id= {AutosellOrderID}");
                 }
             }
             if (firstorder)
             {
                 // İlk emirleri gönderiyoruz
                 SendOrders(t.Price);
                 firstorder = false;
             }
             // Tarih kontrolü yapıyoruz
             //fiyasa emir giriş saati 09:55 olup recalculateMinute girilmişse saat 10 da gonderilen emirleri silip yeniden gonderiyor.//
             //bunu engellemek için saat yazılan emir iptali için saat 10 şartı eklendi. istenmezse "DateTime.Now.Hour >= 10 &&" silinmeli//
             if (DateTime.Now.Hour >= 10 && DateTime.Now > Tarih)
             {
                 if (!isCancellingOrders)
                 {   
                     isCancellingOrders = true; // Emirleri iptal etmeye başlıyoruz
                     // Tüm bekleyen alım emirlerini iptal ediyoruz
                     var buyOrderIds = buyOrders.Keys.ToList(); // Kopyasını alıyoruz
                     foreach (var orderId in buyOrderIds)
                     {
                         try
                         {
                             if (buyOrders.ContainsKey(orderId))
                             {
                                 CancelOrder(orderId);
                             }
                             else
                             {
                                 SendMessage(MessageTypes.Log, $"Order not found during cancellation: ID={orderId}");
                             }
                         }
                         catch (Exception ex)
                         {
                             SendMessage(MessageTypes.Log, $"Failed to cancel order {orderId}: {ex.Message}");
                         }
                     }
                     // Gecikmeli alış emirlerini de kaldırıyoruz
                     delayedOrders.RemoveAll(o => (Directions)o["Direction"] == Directions.BUY);
                     isCancellingOrders = false; // Emirleri iptal etmeyi bitirdik
                     // SendOrders fonksiyonunu tekrar çalıştırıyoruz
                     SendOrders(CurrentPrice);
                     
                     if(recalculateMinute == 0)
                     {
                         DateTime dtnext=DateTime.Now.AddDays(1);
                         Tarih = new DateTime(dtnext.Year,dtnext.Month,dtnext.Day);
                     }
                     else
                     {
                         Tarih = DateTime.Now.AddMinutes(recalculateMinute);
                     }
                 }
             }
        }
    }
     catch (Exception ex)
    {
         SendMessage(MessageTypes.Log, $"Exception in PriceChanged: {ex.Message}");
    }
}
 // Emir durumu değiştiğinde çağrılan fonksiyon 
 public void OrderStatusChanged(Order o)
 {
     try
     {
         if (o != null)
         {
             if (o.Direction == Directions.BUY)
             {
                 if (o.Status == OrderStatus.Filled)
                 {
                     // Alış emri gerçekleşti
                     if (buyOrders.ContainsKey(o.Id))
                     {
                         var orderInfo = buyOrders[o.Id];
                         buyOrders.Remove(o.Id);
                         // Kar al fiyatını hesaplıyoruz
                         double takeProfitPrice = CalculateSellPrice(orderInfo.Price, takeProfitSteps);
                         // Satış emrini gecikmeli olarak planlıyoruz
                         var delayedSellOrder = new Dictionary<string, object>
                         {
                             {"Direction", Directions.SELL},
                             {"Price", takeProfitPrice},
                             {"Quantity", orderInfo.Quantity},
                             {"ExecutionTime", DateTime.Now.AddSeconds(orderdelaysecond)},
                             {"AssociatedBuyOrderId", o.Id},
                             {"OriginalPrice", orderInfo.Price}
                         };
                         delayedOrders.Add(delayedSellOrder);
                     }
                     else
                     {
                         SendMessage(MessageTypes.Log, $"Buy order {o.Id} not found in buyOrders dictionary.");
                     }
                 }
                 else if (o.Status == OrderStatus.Canceled)
                 {
                     if (buyOrders.ContainsKey(o.Id))
                     {
                         buyOrders.Remove(o.Id);
                     }
                     else
                     {
                         SendMessage(MessageTypes.Log, $"Cancelled buy order {o.Id} not found in buyOrders dictionary.");
                     }
                 }
             }
             else if (o.Direction == Directions.SELL)
             {
                 if (o.Status == OrderStatus.Filled)
                 {
                     var positionIndex = filledPositions.FindIndex(p => p.SellOrderID == o.Id);
                     if (positionIndex >= 0)
                     {
                         var position = filledPositions[positionIndex];
                         filledPositions.RemoveAt(positionIndex);
                         if (recalculateMinute == 0)
                         {
                             DateTime dtnext = DateTime.Now.AddDays(1);
                             Tarih = new DateTime(dtnext.Year, dtnext.Month, dtnext.Day);
                         }
                         else
                         {
                             Tarih = DateTime.Now.AddMinutes(recalculateMinute);
                         }
                         // Eğer emirleri iptal etmiyorsak ve bu emir AutosellOrderID'ye ait değilse yeni alış emri planlanıyor
                         if (!isCancellingOrders && o.Id != AutosellOrderID)
                         {
                             double totalHoldingAmount = GetTotalHoldingAmount();
                             if (totalHoldingAmount + (position.Price * position.Quantity) <= MaxAmount)
                             {
                                 var delayedBuyOrder = new Dictionary<string, object>
                                 {
                                     {"Direction", Directions.BUY},
                                     {"Price", position.Price},
                                     {"Quantity", position.Quantity},
                                     {"ExecutionTime", DateTime.Now.AddSeconds(orderdelaysecond)},
                                     {"AssociatedBuyOrderId", null},
                                     {"OriginalPrice", 0.0}
                                 };
                                 delayedOrders.Add(delayedBuyOrder);
                             }
                             else
                             {
                                 SendMessage(MessageTypes.Log, "Total holding amount exceeds MaxAmount, not scheduling new buy order.");
                             }
                         }
                         else if (o.Id == AutosellOrderID)
                         {
                             // Eğer bu dolan emir autoSell emrimiz ise, AutosellOrderID'yi sıfırla
                             AutosellOrderID = "";
                         }
                     }
                     else if (sellOrders.ContainsKey(o.Id))
                     {
                         var sellOrderInfo = sellOrders[o.Id];
                         sellOrders.Remove(o.Id);
                         AdjustFilledPositionsAfterSell((int)o.Lot);
                         if (recalculateMinute == 0)
                         {
                             DateTime dtnext = DateTime.Now.AddDays(1);
                             Tarih = new DateTime(dtnext.Year, dtnext.Month, dtnext.Day);
                         }
                         else
                         {
                             Tarih = DateTime.Now.AddMinutes(recalculateMinute);
                         }
                         // Eğer bu emir AutosellOrderID ise sıfırla
                         if (o.Id == AutosellOrderID)
                         {
                             AutosellOrderID = "";
                         }
                     }
                     else
                     {
                         SendMessage(MessageTypes.Log, $"Sell order {o.Id} not found in filled positions or sellOrders dictionaries.");
                     }
                 }
                 else if (o.Status == OrderStatus.Canceled)
                 {
                     if (sellOrders.ContainsKey(o.Id))
                     {
                         sellOrders.Remove(o.Id);
                     }
                     else
                     {
                         int posIndex = filledPositions.FindIndex(p => p.SellOrderID == o.Id);
                         if (posIndex >= 0)
                         {
                             var position = filledPositions[posIndex];
                             filledPositions[posIndex] = (position.Price, position.Quantity, null);
                             // SendMessage(MessageTypes.Log, $"Cancelled sell order {o.Id}, SellOrderID nulled in filledPositions.");
                         }
                         else
                         {
                             SendMessage(MessageTypes.Log, $"Cancelled sell order {o.Id} not found in sellOrders or filledPositions dictionary.");
                         }
                     }
                     // Eğer iptal edilen emir AutosellOrderID ise onu da sıfırla
                     if (o.Id == AutosellOrderID)
                     {
                         AutosellOrderID = "";
                     }
                 }
             }
         }
     }
     catch (Exception ex)
     {
         SendMessage(MessageTypes.Log, $"Exception in OrderStatusChanged: {ex.Message}");
     }
 }
 // Kar al (satış) fiyatını hesaplayan fonksiyon
 public double CalculateSellPrice(double price, int steps)
 {
     try
     {
         double currentPrice = price;
         for (int i = 0; i < steps; i++)
         {
             double kademe = GetKademe(currentPrice);
             currentPrice += kademe;
             currentPrice = Math.Round(currentPrice / kademe) * kademe;
         }
         return currentPrice;
     }
     catch (Exception ex)
     {
         SendMessage(MessageTypes.Log, $"Exception in CalculateSellPrice: {ex.Message}");
         return price; 
     }
 }
 // Alış fiyatını hesaplayan fonksiyon
 public double CalculateBuyPrice(double price, int step)
 {
     try
     {
         double currentPrice = price;
         for (int i = 0; i < step; i++)
         {
             double kademe = GetKademe(currentPrice);
             currentPrice -= kademe;
             currentPrice = Math.Round(currentPrice / kademe) * kademe;
         }
         return currentPrice;
     }
     catch (Exception ex)
     {
         SendMessage(MessageTypes.Log, $"Exception in CalculateBuyPrice: {ex.Message}");
         return price; 
     }
 }
 // Fiyata göre kademe değerini döndüren fonksiyon
 public double GetKademe(double price)
 {
     try
     {
         if (price < 20) return 0.01;
         else if (price <= 50) return 0.02;
         else if (price <= 100) return 0.05;
         else if (price <= 250) return 0.1;
         else if (price <= 500) return 0.25;
         else if (price <= 1000) return 0.5;
         else if (price <= 2500) return 1;
         else return 2.5;
     }
     catch (Exception ex)
     {
         SendMessage(MessageTypes.Log, $"Exception in GetKademe: {ex.Message}");
         return 0.01;
     }
 }
//GRID alış emirlerinin gönderen fonksiyon 
public void SendOrders(double currentPrice)
 {
     try
     {
         double totalHoldingAmount = GetTotalHoldingAmount();
         if (totalHoldingAmount >= MaxAmount)
         {
             SendMessage(MessageTypes.Log, "MaxAmount reached. Not sending new orders.");
             return;
         }
         for (int i = 1; i <= gridStepCount; i++)
         {
             double targetBuyPrice = CalculateBuyPrice(currentPrice, i * gridSteps);
             int lotMiktari = (int)(Amount / targetBuyPrice);
             if (lotMiktari == 0)
                 lotMiktari = 1;
             totalHoldingAmount += targetBuyPrice * lotMiktari;
             if (totalHoldingAmount > MaxAmount)
             {
                 SendMessage(MessageTypes.Log, "Total holding amount exceeded MaxAmount during order sending.");
                 break;
             }
             var buyOrderID = SendOrder(Symbol2, Directions.BUY, lotMiktari, PriceTypes.Limit, Math.Round(targetBuyPrice,2));
             if (string.IsNullOrEmpty(buyOrderID))
             {
                 SendMessage(MessageTypes.Log, $"Failed to send buy order at price {targetBuyPrice}");
                 continue;
             }
             buyOrders.Add(buyOrderID, (targetBuyPrice, lotMiktari, null));
         }
     }
     catch (Exception ex)
     {
         SendMessage(MessageTypes.Log, $"Exception in SendOrders: {ex.Message}");
     }
 }
 public double GetTotalHoldingAmount()
 {
     try
     {
         double totalAmount = 0;
         foreach (var position in filledPositions)
         {
             totalAmount += position.Price * position.Quantity;
         }
         return totalAmount;
     }
     catch (Exception ex)
     {
         SendMessage(MessageTypes.Log, $"Exception in GetTotalHoldingAmount: {ex.Message}");
         return 0;
     }
 }
 public void ExecuteDelayedOrders()
 {
     try
     {
         var ordersToExecute = delayedOrders.Where(o => DateTime.Now >= (DateTime)o["ExecutionTime"]).ToList();
         foreach (var delayedOrder in ordersToExecute)
         {
             ExecuteDelayedOrder(delayedOrder);
             delayedOrders.Remove(delayedOrder);
         }
     }
     catch (Exception ex)
     {
         SendMessage(MessageTypes.Log, $"Exception in ExecuteDelayedOrders: {ex.Message}");
     }
 }
 public void ExecuteDelayedOrder(Dictionary<string, object> order)
 {
     try
     {
         Directions direction = (Directions)order["Direction"];
         double price = (double)order["Price"];
         int quantity = (int)order["Quantity"];
         if (isCancellingOrders)
         {
             SendMessage(MessageTypes.Log, "Order execution skipped due to cancelling orders.");
             return;
         }
         string orderId = SendOrder(Symbol2, direction, quantity, PriceTypes.Limit, Math.Round(price,2));
         if (string.IsNullOrEmpty(orderId))
         {
             SendMessage(MessageTypes.Log, $"Failed to send delayed {direction} order at price {price}");
             return;
         }
         if (direction == Directions.BUY)
         {
             buyOrders.Add(orderId, (price, quantity, null));
         }
         else if (direction == Directions.SELL)
         {
             string associatedBuyOrderId = (string)order["AssociatedBuyOrderId"];
             double originalPrice = (double)order["OriginalPrice"];
             if (!string.IsNullOrEmpty(associatedBuyOrderId))
             {
                 filledPositions.Add((originalPrice, quantity, orderId));
             }
             else
             {
                 sellOrders.Add(orderId, (price, quantity));
             }
         }
     }
     catch (Exception ex)
     {
         SendMessage(MessageTypes.Log, $"Exception in ExecuteDelayedOrder: {ex.Message}");
     }
 }
 public double GetAverageCost()
 {
     try
     {
         double totalCost = 0;
         int totalQuantity = 0;
         foreach (var position in filledPositions)
         {
             if (string.IsNullOrEmpty(position.SellOrderID))
             {
                 totalCost += position.Price * position.Quantity;
                 totalQuantity += position.Quantity;
             }
         }
         if (totalQuantity > 0)
         {
             return totalCost / totalQuantity;
         }
         else
         {
             return 0;
         }
     }
     catch (Exception ex)
     {
         SendMessage(MessageTypes.Log, $"Exception in GetAverageCost: {ex.Message}");
         return 0;
     }
 }
 public int GetAvailableQuantity()
 {
     try
     {
         int availableQuantity = 0;
         foreach (var position in filledPositions)
         {
             if (string.IsNullOrEmpty(position.SellOrderID))
             {
                 availableQuantity += position.Quantity;
             }
         }
         return availableQuantity;
     }
     catch (Exception ex)
     {
         SendMessage(MessageTypes.Log, $"Exception in GetAvailableQuantity: {ex.Message}");
         return 0;
     }
 }
 public void AdjustFilledPositionsAfterSell(int quantitySold)
 {
     try
     {
         int remainingQuantityToRemove = quantitySold;
         while (remainingQuantityToRemove > 0 && filledPositions.Count > 0)
         {
             var position = filledPositions[0];
             if (position.Quantity <= remainingQuantityToRemove)
             {
                 remainingQuantityToRemove -= position.Quantity;
                 filledPositions.RemoveAt(0);
             }
             else
             {
                 var updatedPosition = (position.Price, position.Quantity - remainingQuantityToRemove, position.SellOrderID);
                 filledPositions[0] = updatedPosition;
                 remainingQuantityToRemove = 0;
             }
         }
         if (remainingQuantityToRemove > 0)
         {
             SendMessage(MessageTypes.Log, $"Attempted to sell more quantity ({quantitySold}) than held positions.");
         }
     }
     catch (Exception ex)
     {
         SendMessage(MessageTypes.Log, $"Exception in AdjustFilledPositionsAfterSell: {ex.Message}");
     }
 }


